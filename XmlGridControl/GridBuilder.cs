//  Copyright (c) 2009, Semyon A. Chertkov (semyonc@gmail.com)
//  All rights reserved.
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU LESSER GENERAL PUBLIC LICENSE as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.
//
//  You should have received a copy of the GNU LESSER GENERAL PUBLIC LICENSE
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace WmHelp.XmlGrid
{
    public class XmlLabelCell: GridCell
    {
        public XmlNode Node { get; private set; }

        public XmlLabelCell(XmlNode node)
        {
            Node = node;
        }

        public override int ImageIndex
        {
            get
            {
                if (Node == null)
                    return -1;
                switch (Node.NodeType)
                {
                    case XmlNodeType.Attribute:
                        return 2;
                    case XmlNodeType.Element:
                        return 3;
                    case XmlNodeType.Text:
                        return 4;
                    case XmlNodeType.CDATA:
                        return 5;
                    case XmlNodeType.Comment:
                        return 6;
                    case XmlNodeType.DocumentType:
                        return 7;
                    default:
                        return -1;
                }
            }
        }

        public override string Text
        {
            get
            {
                if (Node != null)
                    switch (Node.NodeType)
                    {
                        case XmlNodeType.Comment:
                        case XmlNodeType.Text:
                        case XmlNodeType.CDATA:
                            return Node.Value;
                        
                        default:
                            return Node.Name;
                    }
                else
                    return null;
            }
            set
            {
                return;
            }
        }

        public override void DrawCellText(XmlGridView gridView, Graphics graphics, Font font, Brush brush, 
            StringFormat sf, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            if (Node.NodeType != XmlNodeType.Attribute && Node.NodeType != XmlNodeType.Element)
                font = new Font(font, FontStyle.Italic);
            base.DrawCellText(gridView, graphics, font, brush, sf, drawInfo, rect);
        }
    }

    public class XmlValueCell : GridCell
    {
        public XmlNode Node { get; private set; }

        public XmlValueCell(XmlNode node)
        {
            Node = node;
        }

        public override string Text
        {
            get
            {
                if (Node != null)
                {
                    if (Node.NodeType == XmlNodeType.Element)
                        return Node.InnerText;
                    else
                        return Node.Value;
                }
                else
                    return null;
            }
            set
            {
                return;
            }
        }

        public override void DrawCellText(XmlGridView gridView, Graphics graphics, Font font, Brush brush, 
            StringFormat sf, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            if (gridView.AutoHeightCells)
                sf.FormatFlags = sf.FormatFlags & ~StringFormatFlags.NoWrap;
            base.DrawCellText(gridView, graphics, font, brush, sf, drawInfo, rect);
        }

        public override int GetTextHeight(XmlGridView gridView, Graphics graphics, 
            Font font, XmlGridView.DrawInfo drawInfo, int Width)
        {
            if (String.IsNullOrEmpty(Text))
                return drawInfo.cyChar;
            else
            {
                StringFormat sf = GetStringFormat();
                sf.FormatFlags = 0;
                SizeF sz = graphics.MeasureString(Text, font, Width, sf);
                int height = Math.Max((int)sz.Height, drawInfo.cyChar);
                if (height > drawInfo.cyChar)
                    height += 4;
                return height;
            }
        }
    }

    public class XmlDeclarationCell : GridCell
    {
        public XmlNode Node { get; private set; }

        public XmlDeclarationCell(XmlNode node)
        {
            Node = node;
        }

        public override string Text
        {
            get
            {
                if (Node.NodeType == XmlNodeType.DocumentType)
                {
                    XmlDocumentType docType = (XmlDocumentType)Node;
                    if (docType.PublicId != null || docType.SystemId != null)
                    {
                        StringBuilder sb = new StringBuilder();
                        if (docType.PublicId != null)
                            sb.AppendFormat("PUBLIC \"{0}\"", docType.PublicId);
                        if (docType.SystemId != null)
                        {
                            if (sb.Length > 0)
                                sb.Append(' ');
                            sb.AppendFormat("SYSTEM \"{0}\"", docType.SystemId);
                        }
                        return sb.ToString();
                    }
                }
                return Node.Value;
            }
            set
            {
                return;
            }
        }
    }

    public class XmlGroupCell : GridCellGroup
    {
        public XmlElement Node { get; private set; }        

        public XmlGroupCell(XmlNode node)
        {
            Node = (XmlElement)node;
        }

        public override string Text
        {
            get
            {
                return Node.Name;
            }
            set
            {
                return;
            }
        }

        public override string Description
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (Node != null && Node.HasAttributes)
                    for (int i = 0; i < Node.Attributes.Count; i++)
                    {
                        if (sb.Length > 150)
                        {
                            sb.Append("..");
                            break;
                        }
                        if (i > 0)
                            sb.Append(" ");
                        XmlAttribute attr = (XmlAttribute)Node.Attributes.Item(i);
                        sb.AppendFormat("{0}={1}", attr.Name, attr.Value);
                    }
                return sb.ToString();
            }
        }

        public override void BeforeExpand()
        {
            if (Table.IsEmpty)
            {
                GridBuilder builder = new GridBuilder();
                builder.ParseNodes(this, Node.Attributes, Node.ChildNodes);
            }
        }

        public override void CopyToClipboard()
        {
            DataFormats.Format fmt = DataFormats.GetFormat("EXML Fragment");
            DataObject data = new DataObject();
            MemoryStream stream = new MemoryStream();
            StreamWriter sw = new StreamWriter(stream, Encoding.Unicode);
            sw.Write("<doc>");
            if (TableView)
            {
                for (int s = 1; s < Table.Height; s++)
                {
                    XmlRowLabelCell cell = (XmlRowLabelCell)Table[0, s];
                    sw.Write(cell.Node.OuterXml);
                }
            }
            else
                sw.Write(Node.OuterXml);
            sw.Write("</doc>");
            sw.Flush();
            stream.WriteByte(0);
            stream.WriteByte(0);
            data.SetData(fmt.Name, false, stream);
            if (TableView)
            {                
                stream = new MemoryStream();
                sw = new StreamWriter(stream, Encoding.Default);
                String separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
                for (int s = 0; s < Table.Height; s++)
                {
                    if (s > 0)
                        sw.WriteLine();
                    for (int k = 1; k < Table.Width; k++)
                    {
                        if (k > 1)
                            sw.Write(separator);
                        if (Table[k, s] != null && Table[k, s].Text != null)
                        {
                            String text = Table[k, s].Text;
                            if (text.Contains(separator) || text.Contains("\""))
                                text = String.Format("\"{0}\"", text.Replace("\"", "\"\""));
                            sw.Write(text);
                        }
                    }                    
                }
                sw.Flush();
                stream.WriteByte(0);
                stream.WriteByte(0);
                data.SetData(DataFormats.CommaSeparatedValue, false, stream);
            }
            data.SetText(Text);
            Clipboard.SetDataObject(data);
        }
    }

    public class XmlColumnLabelCell : GridColumnLabel
    {
        public XmlNodeType NodeType { get; private set; }
        public String NodeName { get; private set; }
        public int NodePos { get; private set; }

        public XmlColumnLabelCell(Type type, String nodeName, int nodePos)
        {
            if (typeof(XmlAttribute).IsAssignableFrom(type))
                NodeType = XmlNodeType.Attribute;
            else if (typeof(XmlElement).IsAssignableFrom(type))
                NodeType = XmlNodeType.Element;
            else if (typeof(XmlText).IsAssignableFrom(type))
                NodeType = XmlNodeType.Text;
            else if (typeof(XmlCDataSection).IsAssignableFrom(type))
                NodeType = XmlNodeType.CDATA;
            else if (typeof(XmlProcessingInstruction).IsAssignableFrom(type))
                NodeType = XmlNodeType.ProcessingInstruction;
            else
                NodeType = XmlNodeType.None;
            NodeName = nodeName;
            NodePos = nodePos;
        }

        public override int ImageIndex
        {
            get
            {
                switch (NodeType)
                {
                    case XmlNodeType.Attribute:
                        return 2;
                    case XmlNodeType.Element:
                        return 3;
                    case XmlNodeType.Text:
                        return 4;
                    case XmlNodeType.CDATA:
                        return 5;
                    default:
                        return -1;
                }
            }
        }

        public XmlNode GetNodeAtColumn(XmlNode node)
        {
            int nodePos = NodePos;
            if (NodeType == XmlNodeType.Attribute)
            {
                if (node.Attributes != null)
                    return node.Attributes.GetNamedItem(NodeName);
            }
            else
                if (node.HasChildNodes)
                    for (int k = 0; k < node.ChildNodes.Count; k++)
                    {
                        XmlNode child = node.ChildNodes.Item(k);
                        if (child.NodeType == NodeType && 
                            ((NodeType != XmlNodeType.Element || NodeName.Equals(child.Name))))
                            if (nodePos == 0)
                                return child;
                            else
                                nodePos--;
                    }
            return null;
        }

        public override string Text
        {
            get
            {
                switch (NodeType)
                {
                    case XmlNodeType.Text:
                        return "#text";
                    case XmlNodeType.CDATA:
                        return "#CDATA";
                    default:
                        return NodeName;
                }
            }
            set
            {
                base.Text = value;
            }
        }

        public override void DrawCellText(XmlGridView gridView, Graphics graphics, Font font, 
            Brush brush, StringFormat sf, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            if (NodeType != XmlNodeType.Element && NodeType != XmlNodeType.Attribute)
                font = new Font(font, FontStyle.Italic);
            else
                font = new Font(font, FontStyle.Bold);
            base.DrawCellText(gridView, graphics, font, brush, sf, drawInfo, rect);
        }

    }

    public class XmlRowLabelCell : GridRowLabel
    {
        public XmlNode Node { get; private set; }

        public XmlRowLabelCell(int row, XmlNode node)
        {
            RowNum = row;
            Node = node;
        }
    }

    public class GridBuilder
    {
        protected class TableColumn
        {
            public Type type;
            public Type dataType;
            public String name;
            public int pos;
            public int count;
            public bool marked;

            public override bool Equals(object obj)
            {
                if (obj is TableColumn)
                {
                    TableColumn c = (TableColumn)obj;
                    return type == c.type && name == c.name &&
                        pos == c.pos && count == c.count;
                }
                else
                    return false;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        protected class TableColumns : IEnumerable<TableColumn>
        {
            private List<TableColumn> _list;

            public TableColumns()
            {
                _list = new List<TableColumn>();
            }

            public int Length
            {
                get
                {
                    return _list.Count;
                }
            }

            public TableColumn this[int index]
            {
                get
                {
                    return _list[index];
                }
            }

            public TableColumn Last()
            {
                if (Length > 0)
                    return _list[Length - 1];
                else
                    return null;
            }

            public TableColumn Add()
            {
                TableColumn res = new TableColumn();
                _list.Add(res);
                return res;
            }

            public void Add(TableColumn col)
            {
                if (Length > 0)
                {
                    TableColumn last = Last();
                    if (last.type == col.type && last.name == col.name &&
                        last.pos == col.pos)
                        last.count++;
                    else
                        _list.Add(col);
                }
                else
                    _list.Add(col);
            }

            #region IEnumerable<TableColumn> Members

            public IEnumerator<TableColumn> GetEnumerator()
            {
                return _list.GetEnumerator();
            }

            #endregion

            #region IEnumerable Members

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _list.GetEnumerator();
            }

            #endregion
        }


        public class NodeList : XmlNodeList
        {
            private List<XmlNode> _nodes = new List<XmlNode>();

            public override int Count
            {
                get { return _nodes.Count; }
            }

            public override System.Collections.IEnumerator GetEnumerator()
            {
                return _nodes.GetEnumerator();
            }

            public override XmlNode Item(int index)
            {
                return _nodes[index];
            }

            public void Add(XmlNode node)
            {
                _nodes.Add(node);
            }
        }

        protected enum ItemType
        {
            Values,
            List,
            Table
        };

        protected class Item
        {
            public ItemType type;
            public List<XmlNode> nodes;

            public Item()
            {
                nodes = new List<XmlNode>();
            }
        }

        protected class ItemList
        {
            private List<Item> _list;

            public ItemList()
            {
                _list = new List<Item>();
            }

            public int Length
            {
                get
                {
                    return _list.Count;
                }
            }

            public Item this[int index]
            {
                get
                {
                    return _list[index];
                }
            }

            public Item Last()
            {
                if (Length > 0)
                    return _list[Length - 1];
                else
                    return null;
            }

            public XmlNode LastNode()
            {
                Item item = Last();
                if (item != null && item.nodes.Count > 0)
                    return item.nodes[item.nodes.Count - 1];
                else
                    return null;
            }

            private Item GetItem(ItemType type)
            {
                Item item = Last();
                if (item == null || type != item.type)
                {
                    item = new Item();
                    item.type = type;
                    _list.Add(item);
                }
                return item;
            }

            public void Add(ItemType type, XmlNode node)
            {
                Item item = GetItem(type);
                item.nodes.Add(node);
            }

            public void Add(ItemType type, XmlNodeList nodes)
            {
                Item item = GetItem(type);
                for (int k = 0; k < nodes.Count; k++)
                    item.nodes.Add(nodes.Item(k));
            }

            public void Add(ItemType type, XmlAttributeCollection nodes)
            {
                Item item = GetItem(type);
                for (int k = 0; k < nodes.Count; k++)
                    item.nodes.Add(nodes.Item(k));
            }

            public void Add(ItemType type, XmlNamedNodeMap attrs)
            {
                Item item = GetItem(type);
                for (int k = 0; k < attrs.Count; k++)
                    item.nodes.Add(attrs.Item(k));
            }

            public void Fork()
            {
                Item item = Last();
                if (item.nodes.Count > 1)
                {
                    ItemType type = item.type;
                    XmlNode node = LastNode();
                    item.nodes.RemoveAt(item.nodes.Count -1);
                    item = new Item();
                    _list.Add(item);
                    item.type = type;
                    item.nodes.Add(node);
                }
            }

            public int CountCells()
            {
                int res = 0;
                for (int i = 0; i < Length; i++)
                    if (this[i].type == ItemType.List)
                        res += this[i].nodes.Count;
                    else
                        res++;
                return res;
            }
        }

        public static bool IsPairNode(XmlNode node)
        {
            if (node is XmlText || node is XmlAttribute)
                return true;
            else
                if (node is XmlElement)
                {
                    XmlElement elem = (XmlElement)node;
                    if (!elem.HasAttributes && (!elem.HasChildNodes ||
                            (elem.ChildNodes.Count == 1 && elem.FirstChild is XmlText)))
                        return true;
                }
            return false;
        }

        protected bool CanGroupNodes(XmlNode node1, XmlNode node2)
        {
            if (node1 != null && node1 is XmlElement &&
                node2 != null && node2 is XmlElement)
            {
                XmlElement elem1 = (XmlElement)node1;
                XmlElement elem2 = (XmlElement)node2;
                if (elem1.Name == elem2.Name)
                    return true;
            }
            return false;
        }

        protected string GetNodeName(XmlNode node)
        {
            if (node is XmlText && node.ParentNode.ChildNodes.Count == 1)
                return node.ParentNode.Name;
            else
                return node.Name;
        }

        protected List<XmlNode> SelectChilds(XmlNode node)
        {
            List<XmlNode> res = new List<XmlNode>();
            foreach (XmlNode child in node.ChildNodes)
                if (!(child is XmlSignificantWhitespace))
                    res.Add(child);
            return res;
        }

        protected TableColumns CreateColumns(XmlNode node)
        {
            TableColumns columns = new TableColumns();
            if (node is XmlElement)
            {
                XmlElement elem = (XmlElement)node;
                if (elem.HasAttributes)
                    foreach (XmlAttribute attr in elem.Attributes)
                    {
                        TableColumn col = columns.Add();
                        col.type = typeof(XmlAttribute);
                        col.name = attr.Name;
                    }
            }
            if (node.HasChildNodes)
            {
                int i = 0;
                List<XmlNode> childs = SelectChilds(node);
                while (i < childs.Count)
                {
                    XmlNode cur = childs[i];
                    TableColumn col = columns.Add();
                    col.type = cur.GetType();
                    col.name = GetNodeName(cur);
                    col.pos = 0;
                    col.count = 1;
                    int k;
                    for (k = 0; k < i; k++)
                    {
                        cur = childs[k];
                        if (!(cur is XmlSignificantWhitespace))
                            if (cur.GetType() == col.type && (!(cur is XmlElement) || GetNodeName(cur) == col.name))
                                col.pos++;
                    }
                    k = i + 1;
                    while (k < childs.Count)
                    {
                        cur = childs[k];
                        if (!(cur is XmlSignificantWhitespace))
                            if (cur.GetType() == col.type && (!(cur is XmlElement) || GetNodeName(cur) == col.name))
                                col.count++;
                            else
                                break;
                        k++;
                    }
                    i = k;
                }
            }
            return columns;
        }

        protected TableColumns GroupNode(XmlNode node, TableColumns columns)
        {
            TableColumns nodeColumns = CreateColumns(node);
            TableColumns res = new TableColumns();
            if (columns.Length <= nodeColumns.Length)
            {
                for (int i = 0; i < nodeColumns.Length; i++)
                {
                    for (int k = 0; k < columns.Length; k++)
                        if (!columns[k].marked && columns[k].Equals(nodeColumns[i]))
                        {
                            for (int s = 0; s < k - 1; s++)
                                if (!columns[s].marked)
                                {
                                    res.Add(columns[s]);
                                    columns[s].marked = true;
                                }
                            columns[k].marked = true;
                            break;
                        }
                    res.Add(nodeColumns[i]);
                }
                for (int i = 0; i < columns.Length; i++)
                    if (!columns[i].marked)
                        res.Add(columns[i]);
            }
            else
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    for (int k = 0; k < nodeColumns.Length; k++)
                        if (!nodeColumns[k].marked && nodeColumns[k].Equals(columns[i]))
                        {
                            for (int s = 0; s < k - 1; s++)
                                if (!nodeColumns[s].marked)
                                {
                                    res.Add(nodeColumns[s]);
                                    nodeColumns[s].marked = true;
                                }
                            nodeColumns[k].marked = true;
                            break;
                        }
                    res.Add(columns[i]);
                }
                for (int i = 0; i < nodeColumns.Length; i++)
                    if (!nodeColumns[i].marked)
                        res.Add(nodeColumns[i]);
            }
            return res;
        }

        protected NodeList GetNodeAtColumn(XmlNode node, TableColumn col)
        {
            NodeList res = new NodeList();
            if (col.type == typeof(XmlAttribute))
            {
                XmlElement elem = (XmlElement)node;
                if (elem.HasAttributes)
                {
                    XmlNode attr = elem.Attributes.GetNamedItem(col.name);
                    if (attr != null)
                        res.Add(attr);
                }
            }
            else
                if (node.HasChildNodes)
                {
                    int pos = col.pos;
                    int count = col.count;
                    List<XmlNode> childs = SelectChilds(node);
                    for (int k = 0; k < childs.Count; k++)
                    {
                        XmlNode cur = childs[k];
                        if (cur.GetType() == col.type && (!(cur is XmlElement) || cur.Name == col.name))
                            if (pos == 0)
                            {
                                int s = k;
                                while (count > 0 && s < childs.Count)
                                {
                                    cur = childs[s];
                                    if (cur.GetType() == col.type && (!(cur is XmlElement) || cur.Name == col.name))
                                    {
                                        res.Add(cur);
                                        count--;
                                    }
                                    else
                                        break;
                                    s++;
                                }
                            }
                            else
                                pos--;
                    }
                }
            return res;
        }

        public void ParseNodes(GridCellGroup cell, XmlNamedNodeMap attrs, XmlNodeList nodes)
        {
            ItemList items = new ItemList();
            if (attrs != null && attrs.Count > 0)
                items.Add(ItemType.Values, attrs);
            foreach (XmlNode child in nodes)
            {                
                if (child is XmlSignificantWhitespace)
                    continue;
                if (CanGroupNodes(items.LastNode(), child))
                {
                    if (items.Last().type != ItemType.Table)
                    {
                        items.Fork();
                        items.Last().type = ItemType.Table;
                    }
                    items.Add(ItemType.Table, child);
                }
                else
                    if ((child.NodeType != XmlNodeType.Text && IsPairNode(child)) || 
                        child.NodeType == XmlNodeType.XmlDeclaration || 
                        child.NodeType == XmlNodeType.DocumentType ||
                        child.NodeType == XmlNodeType.ProcessingInstruction)
                        items.Add(ItemType.Values, child);
                    else
                        items.Add(ItemType.List, child);
            }
            if (items.Length == 1 && items[0].type == ItemType.Values)
            {
                cell.Table.SetBounds(2, items[0].nodes.Count);
                for (int s = 0; s < items[0].nodes.Count; s++)
                {
                    XmlNode node = items[0].nodes[s];
                    cell.Table[0, s] = new XmlLabelCell(node);
                    cell.Table[1, s] = new XmlValueCell(node);
                }
            }
            else
            {
                int k = 0;
                cell.Table.SetBounds(1, items.CountCells());
                for (int i = 0; i < items.Length; i++)
                {
                    Item item = items[i];
                    switch (item.type)
                    {
                        case ItemType.Values:
                            {
                                GridCellGroup group = new GridCellGroup();
                                group.Flags = GroupFlags.Expanded | GroupFlags.Overlapped;
                                group.Table.SetBounds(2, item.nodes.Count);
                                for (int s = 0; s < item.nodes.Count; s++)
                                {
                                    XmlNode node = item.nodes[s];
                                    group.Table[0, s] = new XmlLabelCell(node);
                                    if (node.NodeType == XmlNodeType.XmlDeclaration ||
                                        node.NodeType == XmlNodeType.DocumentType)
                                        group.Table[1, s] = new XmlDeclarationCell(node);
                                    else
                                        group.Table[1, s] = new XmlValueCell(node);
                                }
                                cell.Table[0, k++] = group;
                            }
                            break;

                        case ItemType.List:
                            for (int s = 0; s < item.nodes.Count; s++)
                                if (item.nodes[s].NodeType == XmlNodeType.Element)
                                    cell.Table[0, k++] = new XmlGroupCell(item.nodes[s]);
                                else
                                    cell.Table[0, k++] = new XmlLabelCell(item.nodes[s]);
                            break;

                        case ItemType.Table:
                            {
                                GridCellGroup group = new XmlGroupCell(item.nodes[0]);
                                group.Flags = group.Flags | GroupFlags.TableView;
                                TableColumns tableColumns = new TableColumns();
                                for (int s = 0; s < item.nodes.Count; s++)
                                    tableColumns = GroupNode(item.nodes[s], tableColumns);
                                group.Table.SetBounds(tableColumns.Length + 1, item.nodes.Count + 1);
                                group.Table[0, 0] = new GridRowLabel();
                                for (int s = 0; s < tableColumns.Length; s++)
                                    group.Table[s + 1, 0] = new XmlColumnLabelCell(tableColumns[s].type, 
                                        tableColumns[s].name, tableColumns[s].pos);
                                for (int s = 0; s < item.nodes.Count; s++)
                                {
                                    XmlNode node = item.nodes[s];
                                    group.Table[0, s + 1] = new XmlRowLabelCell(s + 1, node);
                                    for (int p = 0; p < tableColumns.Length; p++)
                                    {
                                        NodeList nodeList = GetNodeAtColumn(node, tableColumns[p]);
                                        if (nodeList.Count == 0)
                                            group.Table[p + 1, s + 1] = new XmlValueCell(null);
                                        else
                                        {
                                            XmlNode child = nodeList[0];
                                            if (nodeList.Count == 1)
                                            {
                                                if (child.NodeType != XmlNodeType.Element || IsPairNode(child))
                                                    group.Table[p + 1, s + 1] = new XmlValueCell(child);
                                                else
                                                    group.Table[p + 1, s + 1] = new XmlGroupCell(child);
                                            }
                                            else
                                            {
                                                XmlGroupCell childGroup = new XmlGroupCell(child);
                                                childGroup.Flags = GroupFlags.Overlapped | GroupFlags.Expanded;
                                                group.Table[p + 1, s + 1] = childGroup;                                                
                                                ParseNodes(childGroup, null, nodeList);
                                            }
                                        }
                                    }
                                }
                                cell.Table[0, k++] = group;
                            }
                            break;
                    }
                }
            }
        }
    }
}
