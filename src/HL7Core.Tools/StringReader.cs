using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HL7Core.Tools
{
    public class StringReader
    {
        public StringReader(string Source)
        {
            charEnumerator = Source.GetEnumerator();
        }

        private CharEnumerator charEnumerator;
        public CharEnumerator CharEnumerator { get { return charEnumerator; } }

        public void Reset()
        {
            charEnumerator.Reset();
        }

        public Nullable<char> RawReadChar()
        {
            if (charEnumerator.MoveNext())
            {
                return charEnumerator.Current;
            }
            return null;
        }

        private static bool InternalPassFilter(char Char)
        {
            return true;
        }
        private Func<char, bool> passFilter = InternalPassFilter;
        public Func<char, bool> PassFilter { get { return passFilter; } set { passFilter = value; } }

        private static Nullable<char> InternalDecode(char Char)
        {
            return Char;
        }
        private Func<char, Nullable<char>> decode = InternalDecode;
        public Func<char, Nullable<char>> Decode { get { return decode; } set { decode = value; } }

        public Nullable<char> ReadChar()
        {
            Nullable<char> Char;
            do
            {
                Char = RawReadChar();
                if (!Char.HasValue)
                {
                    return null;
                }
            } while (!PassFilter(Char.Value));
            return Decode(Char.Value);
        }

        public string Read(int Length)
        {
            StringBuilder Result = new StringBuilder();
            for (int Counter = 0; Counter < Length; Counter++)
            {
                Nullable<char> Char = ReadChar();
                if (!Char.HasValue)
                {
                    if (Counter == 0)
                    {
                        return null;
                    }
                    return Result.ToString();
                }
                Result.Append(Char.Value);
            }
            return Result.ToString();
        }

        public string ReadToEnd()
        {
            return Read(int.MaxValue);
        }
    }
}
