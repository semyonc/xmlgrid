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
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace WmHelp.XmlGrid
{

    [FlagsAttribute]
    public enum GroupFlags
    {
        Expanded = 1,
        TableView = 2,
        Overlapped = 4,
        Value = 8,
        Merged = 16
    }

    public class GridCell
    {
        public GridCell()
        {
        }

        public GridCellTable Owner { get; internal set; }

        public GridCellGroup Parent
        {
            get
            {
                if (Owner != null)
                    return Owner.Parent;
                else
                    return null;
            }
        }

        public int Index
        {
            get
            {
                return Owner.Width * Row + Col;
            }
        }

        public int Row { get; internal set; }

        public int Col { get; internal set; }

        public virtual bool IsGroup { get { return false; } }

        public virtual String Text
        {
            get
            {
                return null;
            }
            set
            {
                return;
            }
        }

        public virtual int ImageIndex
        {
            get
            {
                return -1;
            }
        }

        public virtual bool CanEdit
        {
            get
            {
                return false;
            }
        }

        public virtual bool CanEditManual
        {
            get
            {
                return false;
            }
        }

        public virtual StringFormat GetStringFormat()
        {
            StringFormat stringFormat = new StringFormat(StringFormat.GenericDefault);
            stringFormat.Alignment = StringAlignment.Near;
            stringFormat.LineAlignment = StringAlignment.Near;
            stringFormat.Trimming = StringTrimming.Character;
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            return stringFormat;
        }

        public virtual int GetTextWidth(XmlGridView gridView, Graphics graphics,
            Font font, XmlGridView.DrawInfo drawInfo)
        {
            SizeF sizeF = graphics.MeasureString(Text, font);
            if (ImageIndex != -1)
                sizeF.Width += drawInfo.cxImage;
            return (int)sizeF.Width;
        }

        public virtual int GetTextHeight(XmlGridView gridView, Graphics graphics,
            Font font, XmlGridView.DrawInfo drawInfo, int Width)
        {
            return drawInfo.cyChar;
        }

        public virtual void DrawCellText(XmlGridView gridView, Graphics graphics,
            Font font, Brush brush, StringFormat sf, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            rect.Y += 2;
            //rect.Height -= 3;
            graphics.DrawString(Text, font, brush, rect, sf);
        }

        public virtual void CopyToClipboard()
        {
            DataObject data = new DataObject();
            data.SetData(typeof(string), Text);
            Clipboard.SetDataObject(data);
        }
    }

    public class GridCellGroup: GridCell
    {
        public GridCellTable Table { get; private set; }

        public GroupFlags Flags { get; set; }

        public int TableWidth { get; set; }

        public int TableHeight { get; set; }

        public int TablePadding { get; set; }

        public GridCellGroup()
        {
            Table = new GridCellTable(this);
        }

        public GridCell FirstChild()
        {
            if ((Flags & GroupFlags.TableView) != 0)
                return Table[0, 1];
            else
                return Table[0, 0];
        }

        public GridCell LastChild()
        {
            return Table[0, Table.Height - 1];
        }

        public override int ImageIndex
        {
            get
            {
                return Expanded ? 0 : 1;
            }
        }

        public virtual void BeforeExpand()
        {
            return;
        }

        public virtual String Description
        {
            get
            {
                return null;
            }
        }

        public override bool IsGroup { get { return true; } }

        public bool Expanded { get { return (Flags & GroupFlags.Expanded) != 0; } }

        public bool Overlaped { get { return (Flags & GroupFlags.Overlapped) != 0; } }

        public bool TableView { get { return (Flags & GroupFlags.TableView) != 0; } }

        public override void DrawCellText(XmlGridView gridView, Graphics graphics, Font font, 
            Brush brush, StringFormat format, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            StringFormat sf = new StringFormat(format);
            Font f = new Font(font, FontStyle.Bold);
            Brush textBrush = new SolidBrush(SystemColors.GrayText);
            sf.LineAlignment = StringAlignment.Center;
            rect.Height = drawInfo.cyChar;
            graphics.DrawString(Text, f, brush, rect, sf);            
            int w = (int)graphics.MeasureString(Text, f).Width + drawInfo.cxCaps / 2;            
            rect.X += w;
            rect.Width -= w;            
            if (TableView)
                graphics.DrawString(String.Format("({0})", Table.Height - 1), 
                    font, textBrush, rect, sf);
            else
                if (!Expanded && !String.IsNullOrEmpty(Description))
                {
                    sf.Trimming = StringTrimming.EllipsisCharacter;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    graphics.DrawString(Description, font, textBrush, rect, sf);
                }
        }
    }
    
    public class GridCellTable
    {
        internal GridCell[,] _cells;

        public GridCellGroup Parent { get; private set; }

        public int Width { get; private set; }
        
        public int Height { get; private set; }

        public int[] ColumnsWidth { get; private set; }

        public int[] RowHeight { get; private set; }

        public int[] RowCount { get; private set; }

        public GridCellTable(GridCellGroup parent)
        {
            Parent = parent;
        }

        public void SetBounds(int width, int height)
        {
            Width = width;
            Height = height;
            ColumnsWidth = new int[width];
            RowHeight = new int[height];
            RowCount = new int[height];
            _cells = new GridCell[width, height];            
        }

        public bool IsEmpty
        {
            get
            {
                return _cells == null;
            }
        }

        public GridCell this[int col, int row]
        {
            get
            {
                return _cells[col, row];
            }
            set
            {
                if (value.Owner != null)
                    throw new InvalidOperationException();
                value.Owner = this;
                value.Col = col;
                value.Row = row;
                _cells[col, row] = value;
            }
        }
    }

    public class GridColumnLabel: GridCell
    {
        public GridColumnLabel()
        {
        }

        public override int ImageIndex
        {
            get
            {
                return 3;
            }
        }

        public override string Text
        {
            get
            {
                return "label";
            }
            set
            {
            }
        }
    }

    public class GridRowLabel: GridCell
    {
        public GridRowLabel()
        {
        }

        public int RowNum { get; protected set; }

        public override string Text
        {
            get
            {
                if (RowNum > 0)
                    return RowNum.ToString();
                else
                    return "";
            }
            set
            {
            }
        }
    }

    public class GridHeadLabel : GridCell
    {
        private string _text;

        public override StringFormat GetStringFormat()
        {
            StringFormat sf = base.GetStringFormat();
            sf.Trimming = StringTrimming.EllipsisCharacter;
            return sf;
        }

        //public override Size MeasureText(XmlGridView gridView, Graphics graphics, 
        //    Font font, XmlGridView.DrawInfo drawInfo, int Width)
        //{
        //    Size sz = base.MeasureText(gridView, graphics, font, drawInfo, Width);
        //    sz.Width += drawInfo.cxChar;
        //    return sz;
        //}

        public override int GetTextWidth(XmlGridView gridView, Graphics graphics, 
            Font font, XmlGridView.DrawInfo drawInfo)
        {
            return base.GetTextWidth(gridView, graphics, font, drawInfo) + drawInfo.cxChar;
        }

        public override void DrawCellText(XmlGridView gridView, Graphics graphics, Font font,
            Brush brush, StringFormat sf, XmlGridView.DrawInfo drawInfo, Rectangle rect)
        {
            rect.X += drawInfo.cxChar;
            rect.Width -= drawInfo.cxChar;
            base.DrawCellText(gridView, graphics, font, 
                brush, sf, drawInfo, rect);
        }

        public override string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
            }
        }
    }
}