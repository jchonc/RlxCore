using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

namespace HL7Core.Tools.Extenders
{
    public static class XmlExtender
    {
        public static XmlDocument LoadXml(string fileName)
        {
            XmlDocument document = new XmlDocument();
            document.Load(fileName);
            return document;
        }

        public static XmlNode CreateRootNode(this XmlDocument document, string name)
        {
            XmlNode rootNode = document.CreateElement(name);
            document.AppendChild(rootNode);
            return rootNode;
        }

        public static XmlCDataSection CreateDocumentCDataSection(this XmlNode node, string data)
        {
            return node.OwnerDocument.CreateCDataSection(data);
        }

        public static XmlNode ImportNode(this XmlNode node, XmlNode externalNode, bool deep)
        {
            return node.OwnerDocument.ImportNode(externalNode, deep);
        }

        public static XmlNode CreateDocumentNode(this XmlNode node, string name)
        {
            return node.OwnerDocument.CreateElement(name);
        }

        public static XmlNode CreateChildNode(this XmlNode node, string name)
        {
            XmlNode newNode = node.CreateDocumentNode(name);
            node.AppendChild(newNode);
            return newNode;
        }

        public static XmlText CreateChildTextNode(this XmlNode node, string text)
        {
            XmlText newNode = node.OwnerDocument.CreateTextNode(text);
            node.AppendChild(newNode);
            return newNode;
        }

        public static bool IsText(this XmlNode node)
        {
            if (node is XmlText)
            {
                return true;
            }
            if (node.ChildNodes.Count != 1)
            {
                return false;
            }
            return node.FirstChild is XmlText;
        }

        public static T GetNode<T>(this XmlNode node, string childName, T defaultValue)
        {
            if (node != null)
            {
                XmlNode childNode = node.SelectSingleNode(childName);
                if (childNode != null)
                {
                    string result = childNode.InnerText;

                    if (result == "\"\"")
                    {
                        return (T)Convert.ChangeType(string.Empty, typeof(T));
                    }
                    else if (string.IsNullOrWhiteSpace(result))
                    {
                        return (T)Convert.ChangeType(null, typeof(T));
                    }
                    else
                    {
                        try
                        {
                            return (T)Convert.ChangeType(childNode.InnerText, typeof(T));

                        }
                        catch
                        {
                            return defaultValue;
                        }
                    }
                }
            }
            return defaultValue;
        }
    }
}
