using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace HL7Core.PersistentQueue
{
    public interface ISqliteQueueManager
    {
        bool IsClosed { get; set; }
        int Count();
        int ProcessAllItems(Action<string> action, CancellationToken stoppingToken);
        void Enqueue(string item);
    }

    public class SqliteQueueManagerSettings
    {
        public string QueueFileName { get; set; }
        public string QueueTableName { get; set; }
    }

    public class SqliteQueueManager: ISqliteQueueManager
    {
        private readonly string _connectionString;
        private readonly string _queueTableName;
        const string _queueColumnName = "HL7PACKAGE";

        private readonly SqliteQueueManagerSettings _settings;
        public bool IsClosed { get; set; }
        public SqliteQueueManager(IOptions<SqliteQueueManagerSettings> settings)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _connectionString = String.Format("Data Source={0};Mode=ReadWriteCreate;", _settings.QueueFileName);
            _queueTableName = _settings.QueueTableName;
            EnsureTable();
        }

        /// <summary>
        /// Create the required table if needed
        /// </summary>
        protected void EnsureTable()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"CREATE TABLE IF NOT EXISTS {_queueTableName} (id INTEGER PRIMARY KEY AUTOINCREMENT, {_queueColumnName} TEXT )";
                    using( var cmd = new SqliteCommand(stmt, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the queue.
        /// </summary>
        /// <returns>Number of elements.</returns>
        public int Count()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"SELECT COUNT(*) FROM {_queueTableName}";
                    using (var command = new SqliteCommand(stmt, connection))
                    {
                        return Int32.Parse(command.ExecuteScalar().ToString());
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Removes all objects from the queue.
        /// </summary>
        public void Clear()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"DELETE FROM {_queueTableName}";
                    using (var command = new SqliteCommand(stmt, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Determines whether an element is in the queue.
        /// </summary>
        /// <param name="item">
        /// The object to locate in the queue.
        /// </param>
        /// <returns>
        /// true if item is found in the queue; otherwise, false.
        /// </returns>
        public bool Contains(string item)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"SELECT COUNT(*) FROM {_queueTableName} WHERE {_queueColumnName} = ?";
                    using (var command = new SqliteCommand(stmt, connection))
                    {
                        SqliteParameter paramPackage = new SqliteParameter();
                        paramPackage.DbType = System.Data.DbType.String;
                        paramPackage.Value = item.ToString();
                        command.Parameters.Add(paramPackage);

                        return Int32.Parse(command.ExecuteScalar().ToString()) > 0;
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the queue.
        /// </summary>
        /// <returns>
        /// The object that is removed from the beginning of the queue.
        /// </returns>
        public string Dequeue()
        {
            Int64 rowId = -1;
            string queueItem = null;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"SELECT id, {_queueColumnName} FROM {_queueTableName} WHERE ROWID = (SELECT MIN(ROWID) FROM {_queueTableName})";
                    using (var cmdRead = new SqliteCommand(stmt, connection))
                    {
                        using (var reader = cmdRead.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                rowId = reader.GetInt64(0);
                                queueItem = reader.GetString(1);
                                reader.Close();
                            }
                        }
                    }
                    if ( rowId >= 0 )
                    {
                        var stmtDelete = $"DELETE FROM {_queueTableName} WHERE {1} = {rowId}";
                        using (var cmdDelete = new SqliteCommand(stmtDelete, connection))
                        {
                            cmdDelete.ExecuteNonQuery();
                        }
                        return queueItem;
                    }
                    else
                    {
                        return null;
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public List<string> Dequeue(int numberOfItems)
        {
            List<string> results = new List<string>();
            Int64 maxItemId = -1;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try {
                    var stmtRead = $"SELECT id, {_queueColumnName} FROM {_queueTableName} ORDER BY id LIMIT {numberOfItems}";
                    using (SqliteCommand command = new SqliteCommand(stmtRead, connection))
                    {
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            results.Add(reader.GetString(1));
                            maxItemId = reader.GetInt64(0);
                        }
                    }
                    if(maxItemId > 0)
                    {
                        var stmtRemove = $"DELETE FROM {_queueTableName} WHERE id <= {maxItemId}";
                        using( var cmdRemove = new SqliteCommand(stmtRemove, connection))
                        {
                            cmdRemove.ExecuteNonQuery();
                        }
                    }
                    return results;
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Adds an object to the end of the queue.
        /// </summary>
        /// <param name="item">
        /// The object to add to the queue.
        /// </param>
        public void Enqueue(string item)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"INSERT INTO {_queueTableName}({_queueColumnName}) VALUES(?);";
                    using (var command = new SqliteCommand(stmt, connection))
                    {
                        SqliteParameter paramPackage = new SqliteParameter();
                        paramPackage.DbType = System.Data.DbType.String;
                        paramPackage.Value = item.ToString();
                        command.Parameters.Add(paramPackage);
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Returns the object at the beginning of the queue without removing it.
        /// </summary>
        /// <returns>
        /// The object at the beginning of the queue.
        /// </returns>
        public string Peek()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"SELECT {_queueColumnName} FROM {_queueTableName} WHERE ROWID = (SELECT MIN(ROWID) FROM {_queueTableName})";
                    using (SqliteCommand command = new SqliteCommand(stmt, connection))
                    {
                        object queueItem = command.ExecuteScalar();
                        return (queueItem == null) ? null : (string)queueItem;
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Peek the top N items without dequeue them
        /// </summary>
        /// <param name="numberOfItems">the cound of entries to peek</param>
        /// <returns>the list of items</returns>
        public List<string> Peek(int numberOfItems)
        {
            //object queueItem = null;
            List<string> queueItems = new List<string>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                try
                {
                    var stmt = $"SELECT {_queueColumnName} FROM {_queueTableName} ORDER BY id LIMIT {numberOfItems}";
                    using (SqliteCommand command = new SqliteCommand(stmt, connection))
                    {
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            queueItems.Add(reader.GetString(0));
                        }
                        return queueItems;
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        /// <summary>
        /// Performs specified action on all queue items.
        /// </summary>
        /// <param name="action">Action delegate.</param>
        public int ProcessAllItems(Action<string> action, CancellationToken stoppingToken)
        {
            int readyForDequeue = 0;

            var items = Peek(2048);            
            foreach (var item in items)
            {
                try
                {
                    action(item);
                    readyForDequeue++;
                    if ( stoppingToken.IsCancellationRequested )
                    {
                        break;
                    }
                }
                catch (SqlException) // If downstream database is down.... 
                {
                    if (readyForDequeue > 0)
                    {
                        Dequeue(readyForDequeue);
                    }
                    throw;
                }
            }

            if (readyForDequeue > 0)
            {
                Dequeue(readyForDequeue);
            }
            return items.Count;
        }
    }
}
