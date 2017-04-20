using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Data.Xml.Dom;

namespace Podcasts
{
    public static class XmlTools
    {
        public static void AddChildWithInnerText(this Windows.Data.Xml.Dom.XmlElement rootNode, string name, string innerText)
        {
            var node = rootNode.OwnerDocument.CreateElement(name);
            node.InnerText = innerText;
            rootNode.AppendChild(node);
        }

        public static void AddAttribute(this Windows.Data.Xml.Dom.XmlElement node, string name, string value)
        {
            node.SetAttribute(name, value);
        }

        public static string GetChildNodeTextValue(this Windows.Data.Xml.Dom.XmlElement node, string childName, string defaultValue)
        {
            var child = node.ChildNodes.FirstOrDefault(n => n.NodeName == childName);

            if (child == null)
            {
                return defaultValue;
            }

            return child.InnerText;
        }

        public static string GetAttributeValue(this Windows.Data.Xml.Dom.XmlElement node , string attributeName)
        {
            return node.GetAttribute(attributeName);
        }

        public static string GetChildNodeAttribute(this Windows.Data.Xml.Dom.XmlElement node, string childName, string attributeName, string defaultValue)
        {
            var child = node.ChildNodes.FirstOrDefault(n => n.NodeName == childName);

            if (child == null)
            {
                return defaultValue;
            }

            var xmlElement = child as Windows.Data.Xml.Dom.XmlElement;
            return xmlElement?.GetAttribute(attributeName);
        }

        public static Windows.Data.Xml.Dom.XmlElement GetChildByName(this Windows.Data.Xml.Dom.XmlElement node, string childName)
        {
            return (Windows.Data.Xml.Dom.XmlElement) node.ChildNodes.FirstOrDefault(n =>
            {
                var element = n as Windows.Data.Xml.Dom.XmlElement;

                if (element == null)
                {
                    return false;
                }

                return n.NodeName == childName;
            });
        }
    }
}
