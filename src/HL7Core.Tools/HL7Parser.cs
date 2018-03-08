using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using HL7Core.Tools.Extenders;

namespace HL7Core.Tools
{
    public class HL7Parser
    {
        public static HL7Parser LoadPackage(string packet)
        {
            var parser = new HL7Parser();
            parser.InternalLoadPackage(packet);
            return parser;
        }

        public static HL7Parser LoadDocument(string fileName)
        {
            var parser = new HL7Parser();
            parser._documentContainer = new XmlDocument();
            parser._documentContainer.Load(fileName);
            return parser;
        }

        public static HL7Parser LoadDocument(XmlDocument document)
        {
            var parser = new HL7Parser();
            parser._documentContainer = document;
            return parser;
        }

        private void InternalLoadPackage(string packet)
        {
            StringReader packetReader = new StringReader(packet);
            if (packetReader.Read(3) != "MSH") //The package should start with this sequence
            {
                throw new Exception("Invalid package. Missing MSH");
            }
            _fieldSeparator = packetReader.ReadChar().Value; //The first character after MSH is the field separator
            _componentSeparator = packetReader.ReadChar().Value;
            _fieldRepeatSeparator = packetReader.ReadChar().Value;
            _escapeCharacter = packetReader.ReadChar().Value;
            _subComponentSeparator = packetReader.ReadChar().Value;
            if (packetReader.ReadChar().Value != _fieldSeparator)
            {
                throw new Exception("Invalid package. Erroneous package encoding section");
            }

            packetReader.PassFilter = delegate (char Char)
            {
                return (Char != '\n');
            };
            packetReader.Decode = delegate (char Char)
            {
                if (Char == _escapeCharacter)
                {
                    return packetReader.RawReadChar();
                }
                return Char;

            };
            string cleanAndDecodedPackage = "MSH" + _fieldSeparator + "null" + _fieldSeparator + _fieldSeparator + packetReader.ReadToEnd();

            _documentContainer = new XmlDocument();
            XmlNode rootNode = _documentContainer.CreateRootNode("HL7Message");

            XmlNode encodingNode = rootNode.CreateChildNode("Encoding");
            encodingNode.CreateChildNode("FieldSeparator").InnerText = _fieldSeparator.ToString();
            encodingNode.CreateChildNode("ComponentSeparator").InnerText = _componentSeparator.ToString();
            encodingNode.CreateChildNode("FieldRepeatSeparator").InnerText = _fieldRepeatSeparator.ToString();
            encodingNode.CreateChildNode("EscapeCharacter").InnerText = _escapeCharacter.ToString();
            encodingNode.CreateChildNode("SubComponentSeparator").InnerText = _subComponentSeparator.ToString();

            string[] segments = cleanAndDecodedPackage.Split(new char[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                AddSegmentTo(rootNode, segment);
            }
        }

        XmlDocument _documentContainer;

        private char _fieldSeparator;
        private char _componentSeparator;
        private char _fieldRepeatSeparator;
        private char _escapeCharacter;
        private char _subComponentSeparator;

        private void AddSegmentTo(XmlNode rootNode, string segmentString)
        {
            string[] Fields = segmentString.Split(_fieldSeparator);
            string SegmentName = Fields.First();
            XmlNode Segment = rootNode.CreateChildNode(SegmentName);
            int FirstDataFieldIndex = (SegmentName == "MSH") ? 2 : 1;
            for (int FieldIndex = FirstDataFieldIndex; FieldIndex < Fields.Length; FieldIndex++)
            {
                AddFieldTo(Segment, FieldIndex, Fields[FieldIndex]);
            }
        }

        private void AddFieldTo(XmlNode segment, int fieldIndex, string fieldString)
        {
            string[] repeats = fieldString.Split(_fieldRepeatSeparator);
            foreach (string repeat in repeats)
            {
                AddComponentTo(segment.CreateChildNode(segment.Name + "." + fieldIndex), repeat);
            }
        }

        private void AddComponentTo(XmlNode field, string componentString)
        {
            string[] components = componentString.Split(_componentSeparator);
            if (components.Length == 1)
            {
                AddSubComponentTo(field, componentString);
            }
            else
            {
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    AddSubComponentTo(field.CreateChildNode(field.Name + "." + componentIndex), components[componentIndex]);
                }
            }
        }

        private void AddSubComponentTo(XmlNode component, string subComponentString)
        {
            string[] subComponents = subComponentString.Split(_subComponentSeparator);
            if (subComponents.Length == 1)
            {
                component.InnerText = subComponentString;
            }
            else
            {
                for (int subComponentIndex = 0; subComponentIndex < subComponents.Length; subComponentIndex++)
                {
                    component.CreateChildNode(component.Name + "." + subComponentIndex).InnerText = subComponents[subComponentIndex];
                }
            }
        }

        public XmlDocument ToXml()
        {
            return _documentContainer;
        }

        private char[] _separatorByDepth;

        private string Encode(string value)
        {
            return value;
        }

        private void AppendNodeTo(StringBuilder HL7Packet, XmlNode node, int depth)
        {
            string previousNodeName = null;
            foreach (XmlNode child in node.ChildNodes)
            {
                if (previousNodeName != null)
                {
                    HL7Packet.Append((depth == 0) && (previousNodeName == child.Name) ? _fieldRepeatSeparator : _separatorByDepth[depth]);
                }
                if (!child.HasChildNodes || child.IsText())
                {
                    HL7Packet.Append(Encode(child.InnerText));
                }
                else
                {
                    AppendNodeTo(HL7Packet, child, depth + 1);
                }
                previousNodeName = child.Name;
            }
        }

        public string ToHL7()
        {
            XmlNode rootNode = _documentContainer.DocumentElement;
            XmlNode encodingNode = rootNode.SelectSingleNode("Encoding");
            if (encodingNode == null)
            {
                _fieldSeparator = '|';
                _componentSeparator = '^';
                _fieldRepeatSeparator = '~';
                _escapeCharacter = '\\';
                _subComponentSeparator = '&';
            }
            else
            {
                _fieldSeparator = encodingNode.GetNode<string>("FieldSeparator", "|")[0];
                _componentSeparator = encodingNode.GetNode<string>("ComponentSeparator", "^")[0];
                _fieldRepeatSeparator = encodingNode.GetNode<string>("FieldRepeatSeparator", "~")[0];
                _escapeCharacter = encodingNode.GetNode<string>("EscapeCharacter", "\\")[0];
                _subComponentSeparator = encodingNode.GetNode<string>("SubComponentSeparator", "&")[0];
            }

            _separatorByDepth = new char[] { _fieldSeparator, _componentSeparator, _subComponentSeparator };

            XmlNode nodeMSH = rootNode.SelectSingleNode("MSH");

            StringBuilder HL7Packet = new StringBuilder();

            foreach (XmlNode node in rootNode.ChildNodes)
            {
                if (node == encodingNode)
                {
                    continue;
                }
                HL7Packet.Append(node.Name).Append(_fieldSeparator);
                if (node == nodeMSH)
                {
                    HL7Packet.Append(new String(new char[] { _componentSeparator, _fieldRepeatSeparator, _escapeCharacter, _subComponentSeparator, _fieldSeparator }));
                }
                AppendNodeTo(HL7Packet, node, 0);
                HL7Packet.Append(Convert.ToChar(13));
            }
            return HL7Packet.ToString();
        }
    }
}
