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
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Drawing.Text;

namespace WmHelp.XmlGrid
{
    public class XmlGridView: Control
    {
        public struct DrawInfo
        {
            public int cxImage;
            public int cxChar;
            public int cxCaps;
            public int cyChar;
            public int nLinesPerPage;
            public int iMaxWidth;
            public int iHeight;
            public int iPageWidth;
            public int nLinesCount;
            public int nFooterLines;
            public int xPos;
            public int yPos;
        }

        public enum HitTest
        {
            Nothing, 
            Icon, 
            Text, 
            Cell
        }

        internal class ImplicitHScrollBar : HScrollBar
        {
            public ImplicitHScrollBar()
            {
                SetStyle(ControlStyles.Selectable, false);
            }
        }

        internal class ImplicitVScrollBar : VScrollBar
        {
            public ImplicitVScrollBar()
            {
                SetStyle(ControlStyles.Selectable, false);
            }
        }

        private static ImageList _images;
        private VScrollBar _vScroll;
        private HScrollBar _hScroll;

        public int DefaultColumnWidth { get; set; }
        public bool AutoHeightCells { get; set; }
        public bool ShowColumnHeader { get; set; }
        public String[] Footer { get; set; }

        private GridCellGroup _rootCell;
        private DrawInfo _drawInfo;
        private GridCell _focusedCell;
        private int _rowNumWidth;
        private int _columnIndex;
        private int[] _columnsWidth;
        private int[] _rowHeight;
        private bool _resizeFlag;

        public GridCell FocusedCell
        {
            get
            {
                return _focusedCell;
            }
        }

        #region Control

        static XmlGridView()
        {
            _images = new ImageList();
            _images.TransparentColor = Color.Fuchsia;
            _images.ImageSize = new Size(16, 13);
            _images.ColorDepth = ColorDepth.Depth24Bit;
            _images.Images.AddStrip(Properties.Resources.Images);
        }

        public XmlGridView()
        {
            Font = new Font("Arial", 8);
            BackColor = SystemColors.Window;
            DefaultColumnWidth = 105;
            _columnsWidth = new int[0];
            _rowHeight = new int[0];
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.Selectable, true);
            _vScroll = new ImplicitVScrollBar();
            _vScroll.Dock = DockStyle.Right;
            _vScroll.ValueChanged += new EventHandler(HandleScrollBar);
            _vScroll.Width = SystemInformation.VerticalScrollBarWidth;
            _vScroll.Visible = false;
            _hScroll = new ImplicitHScrollBar();
            _hScroll.Dock = DockStyle.Bottom;
            _hScroll.Height = SystemInformation.HorizontalScrollBarHeight;
            _hScroll.Visible = false;
            _hScroll.ValueChanged += new EventHandler(HandleScrollBar);
            Controls.AddRange(new Control[] { _vScroll, _hScroll });
        }

        protected override void CreateHandle()
        {
            base.CreateHandle();
            UpdateTextMetrics();
            MeasureCells();
            UpdateScrollRange();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated)
            {
                UpdateScrollRange();
                Invalidate();
            }
        }

        public void Clear()
        {
            _focusedCell = null;
            _rootCell = null;
            UpdateTextMetrics();
            MeasureCells();
            UpdateScrollRange();
            Invalidate();
        }

        public GridCellGroup Cell
        {
            get
            {
                return _rootCell;
            }

            set
            {
                _rootCell = value;
                if (_rootCell != null)
                    _rootCell.Flags |= GroupFlags.Expanded | GroupFlags.Overlapped;
                if (IsHandleCreated)
                {
                    UpdateTextMetrics();
                    MeasureCells();
                    UpdateScrollRange();
                    Invalidate();
                }
            }
        }



        #endregion

        #region Measure Cells
        protected int ColumnWidth(int col)
        {
            if (col < _columnsWidth.Length)
                return _columnsWidth[col];
            else
                return 0;
        }

        protected int RowHeight(int row)
        {
            if (row < _rowHeight.Length)
                return _rowHeight[row];
            else
                return 0;
        }

        protected int RangeWidth(int col, int count)
        {
            int res = 0;
            for (int k = col; k < col + count; k++)
                res += ColumnWidth(k);
            return res;
        }

        protected int RangeHeight(int row, int count)
        {
            int res = 0;
            for (int k = row; k < row + count; k++)
                res += RowHeight(k);
            return res;
        }

        protected void GetRowNumWidth(GridCellTable table, Graphics g)
        {            
            Font font = new Font(Font, FontStyle.Bold);
            int width = (int)g.MeasureString(table.Height.ToString() + "0", font).Width + 3;
            if (width > _rowNumWidth)
                _rowNumWidth = width;
            for (int k = 0; k < table.Width; k++)
                for (int s = 0; s < table.Height; s++)
                    if (table[k, s].IsGroup)
                    {
                        GridCellGroup cell = (GridCellGroup)table[k, s];
                        if (cell.TableView)
                        {
                            width = (int)g.MeasureString(cell.Table.Height.ToString() + "0", font).Width + 3;
                            if (width > _rowNumWidth)
                                _rowNumWidth = width;
                        }
                        GetRowNumWidth(cell.Table, g);
                    }
        }

        protected int CountCellColumns(GridCell cell)
        {
            if (cell.IsGroup)
            {
                GridCellGroup group = (GridCellGroup)cell;
                if (group.Expanded)
                {
                    GridCellTable table = group.Table;
                    int res = 0;
                    for (int k = 0; k < table.Width; k++)
                    {
                        int cols = 1;
                        for (int s = 0; s < table.Height; s++)
                            cols = Math.Max(cols, CountCellColumns(table[k, s]));
                        res += cols;
                    }
                    if (! (group.TableView || group.Overlaped))
                        res++;
                    return res;
                }
            }
            return 1;
        }

        protected int CountCellRows(GridCell cell)
        {
            if (cell.IsGroup)
            {
                GridCellGroup group = (GridCellGroup)cell;
                if (group.Expanded)
                {
                    GridCellTable table = group.Table;
                    int res = 0;
                    for (int s = 0; s < table.Height; s++)
                    {
                        int rows = 1;
                        for (int k = 0; k < table.Width; k++)
                            rows = Math.Max(rows, CountCellRows(table[k, s]));
                        res += rows;
                    }
                    if (!group.Overlaped)
                        res++;
                    return res;
                }
            }
            return 1;
        }

        protected void SetCellWidth(int column, int count, GridCellGroup cell)
        {
            if (cell.Expanded)
            {
                int col = column;
                if (cell.TableView)
                {
                    int cols = CountCellColumns(cell);
                    cell.TableWidth = _rowNumWidth + RangeWidth(column + 1, cols - 1);
                    cell.TablePadding = RangeWidth(column + cols, count - cols);
                }
                else
                {
                    if (cell.Overlaped)
                        cell.TableWidth = RangeWidth(column, count);
                    else
                    {
                        cell.TableWidth = RangeWidth(column + 1, count - 1);
                        col++;
                    }
                    cell.TablePadding = 0;
                }
                for (int k = 0; k < cell.Table.Width; k++)
                    if (k == 0 && cell.TableView)
                    {
                        cell.Table.ColumnsWidth[0] = _rowNumWidth;
                        col++;
                    }
                    else
                    {
                        int cols;
                        if (k < cell.Table.Width - 1 || cell.TableView)
                        {
                            cols = 1;
                            for (int s = 0; s < cell.Table.Height; s++)
                                cols = Math.Max(cols, CountCellColumns(cell.Table[k, s]));
                        }
                        else
                            cols = count - col + column;
                        cell.Table.ColumnsWidth[k] = RangeWidth(col, cols);
                        for (int s = 0; s < cell.Table.Height; s++)
                            if (cell.Table[k, s].IsGroup)
                                SetCellWidth(col, cols, (GridCellGroup)cell.Table[k, s]); 
                        col += cols;
                    }
            }
        }

        protected void SetTableRows(GridCellGroup cell)
        {
            if (cell.Expanded)
                for (int s = 0; s < cell.Table.Height; s++)
                {
                    cell.Table.RowCount[s] = 1;
                    for (int k = 0; k < cell.Table.Width; k++)
                    {
                        cell.Table.RowCount[s] = Math.Max(cell.Table.RowCount[s],
                            CountCellRows(cell.Table[k, s]));
                        if (cell.Table[k, s].IsGroup)
                            SetTableRows((GridCellGroup)cell.Table[k, s]);
                    }
                }
        }

        protected void DoAutoHeightCells(int row, int count, GridCellGroup cell, Graphics g)
        {
            if (cell.Expanded)
            {
                if (!cell.Overlaped)
                    row++;
                for (int s = 0; s < cell.Table.Height; s++)
                {
                    int rows = cell.Table.RowCount[s];
                    int height = RangeHeight(row, rows);
                    for (int k = 0; k < cell.Table.Width; k++)
                        if (cell.Table[k, s].IsGroup)
                            DoAutoHeightCells(row, rows, (GridCellGroup)cell.Table[k, s], g);
                        else
                        {
                            int cheight = cell.Table[k, s].GetTextHeight(this, g, Font, 
                                _drawInfo, cell.Table.ColumnsWidth[k]);
                            if (height < cheight)
                            {
                                _rowHeight[row] += cheight - height;
                                height = RangeHeight(row, rows);
                            }
                        }
                    row += rows;       
                }
            }
        }

        protected void SetCellHeight(int row, int count, GridCellGroup cell)
        {
            if (cell.Expanded)
            {
                if (cell.Overlaped)
                    cell.TableHeight = RangeHeight(row, count);
                else
                {
                    cell.TableHeight = RangeHeight(row + 1, count - 1);
                    row++;
                }
                for (int s = 0; s < cell.Table.Height; s++)
                {
                    int rows = cell.Table.RowCount[s];
                    cell.Table.RowHeight[s] = RangeHeight(row, rows);
                    for (int k = 0; k < cell.Table.Width; k++)
                        if (cell.Table[k, s].IsGroup)
                            SetCellHeight(row, rows, (GridCellGroup)cell.Table[k, s]);
                    row += rows;
                }
            }
        }

        protected void MeasureCells()
        {
            if (_rootCell == null)
            {
                _drawInfo.nLinesCount = 0;
                _drawInfo.iMaxWidth = 0;    
                return;
            }
            Graphics g = CreateGraphics();
            GridCellTable table = _rootCell.Table;
            _rowNumWidth = 30;                        
            GetRowNumWidth(table, g);
            int numcols = 0;
            for (int k = 0; k < table.Width; k++)
            {
                int maxcols = 0;            
                for (int s = 0; s < table.Height; s++)
                    maxcols = Math.Max(maxcols, CountCellColumns(table[k, s]));
                numcols += maxcols;
            }
            int[] columnsWidth = _columnsWidth;
            _columnsWidth = new int[numcols];
            if (numcols == 1)
                _columnsWidth[0] = DefaultColumnWidth * 2;
            else
            {
                for (int k = 0; k < _columnsWidth.Length && k < columnsWidth.Length; k++)
                    _columnsWidth[k] = columnsWidth[k];
                for (int k = columnsWidth.Length; k < numcols; k++)
                    _columnsWidth[k] = DefaultColumnWidth;
            }
            int numrows = 0;
            for (int s = 0; s < table.Height; s++)
            {
                int maxrows = 0;
                for (int k = 0; k < table.Width; k++)
                    maxrows = Math.Max(maxrows, CountCellRows(table[k, s]));
                numrows += maxrows;
            }
            _rowHeight = new int[numrows];
            UpdateWidth();
            UpdateHeight(g);
            g.Dispose();            
        }

        protected void UpdateWidth()
        {
            _drawInfo.iMaxWidth = RangeWidth(0, _columnsWidth.Length);
            SetCellWidth(0, _columnsWidth.Length, _rootCell);
        }

        protected void UpdateHeight(Graphics g)
        {
            for (int s = 0; s < _rootCell.Table.Height; s++)
            {
                _rootCell.Table.RowCount[s] = 0;
                for (int k = 0; k < _rootCell.Table.Width; k++)
                {
                    _rootCell.Table.RowCount[s] = Math.Max(_rootCell.Table.RowCount[s],
                        CountCellRows(_rootCell.Table[k, s]));
                    if (_rootCell.Table[k, s].IsGroup)
                        SetTableRows((GridCellGroup)_rootCell.Table[k, s]);
                }
            }
            for (int s = 0; s < _rowHeight.Length; s++)
                _rowHeight[s] = _drawInfo.cyChar;
            if (AutoHeightCells)
                DoAutoHeightCells(0, _rowHeight.Length, _rootCell, g);
            _drawInfo.iHeight = RangeHeight(0, _rowHeight.Length);
            _drawInfo.nLinesCount = _drawInfo.iHeight / _drawInfo.cyChar;
            SetCellHeight(0, _rowHeight.Length, _rootCell);
        }

        private void UpdateTextMetrics()
        {
            Win32.TEXTMETRIC tm;
            using (Graphics g = CreateGraphics())
            {
                IntPtr hdc = g.GetHdc();
                IntPtr hFont = Win32.SelectObject(hdc, Font.ToHfont());
                tm = Win32.GetTextMetrics(hdc);
                g.ReleaseHdc();
                Win32.DeleteObject(hFont);
            }
            _drawInfo.cxImage = 12;
            _drawInfo.cxChar = tm.tmAveCharWidth;
            if ((tm.tmPitchAndFamily & 1) == 1)
                _drawInfo.cxCaps = 3 * _drawInfo.cxChar / 2;
            else
                _drawInfo.cxCaps = 2 * _drawInfo.cxChar / 2;
            _drawInfo.cyChar = tm.tmHeight + tm.tmExternalLeading + 4;
            if (Footer != null)
                _drawInfo.nFooterLines = Footer.Length + 1;
        }

        #endregion

        #region Cell Navigation

        public GridCell FrontCell(GridCell cell)
        {
            if (cell != null && cell.IsGroup)
            {
                GridCellGroup group = (GridCellGroup)cell;
                if (group.Overlaped)
                    return FrontCell(group.FirstChild());
            }
            return cell;
        }

        public GridCell BottomCell(GridCell cell)
        {
            if (cell != null && cell.IsGroup)
            {
                GridCellGroup group = (GridCellGroup)cell;
                if (group.Expanded)
                    if (group.Overlaped)
                        return BottomCell(group.LastChild());
                    else
                        return FrontCell(group);
            }
            return cell;
        }

        public GridCell PriorCell(GridCell cell)
        {
            if (cell != null)
            {
                if (cell.Row == 0)
                {
                    if (cell.Parent != _rootCell)
                        if (cell.Parent.Overlaped)
                            return BottomCell(PriorCell(cell.Parent));
                        else
                            return cell.Parent;
                    else
                        return null;
                }
                else
                    if (ShowColumnHeader && cell.Parent == _rootCell && cell.Row == 1)
                        return null;
                    else
                        return BottomCell(cell.Owner[cell.Col, cell.Row - 1]);
            }
            else
                return null;

        }

        public GridCell NextCell(GridCell cell, bool canEnter)
        {
            if (cell != null)
            {
                if (cell.IsGroup)
                {
                    GridCellGroup group = (GridCellGroup)cell;
                    if (group.Expanded && canEnter)
                        return FrontCell(group.FirstChild());
                }
                if (cell.Row == cell.Owner.Height - 1)
                {
                    if (cell.Parent != _rootCell)
                        return NextCell(cell.Parent, false);
                    else
                        return null;
                }
                else
                    return FrontCell(cell.Owner[cell.Col, cell.Row + 1]);
            }
            else
                return null;
        }

        public GridCell ForwardCell(GridCell cell)
        {
            GridCell curr = cell;
            while (curr.Parent != null)
                if (curr.Col < curr.Owner.Width - 1)
                    return FrontCell(curr.Owner[curr.Col + 1, curr.Row]);
                else
                    curr = curr.Parent;
            return null;
        }

        public GridCell BackwardCell(GridCell cell)
        {
            if (cell.Col > 0)
                return FrontCell(cell.Owner[cell.Col - 1, cell.Row]);
            else
            {
                GridCell res = cell;
                while (res.Parent != _rootCell && res.Parent != null && 
                       res.Parent.Overlaped)
                    res = res.Parent;
                return res;
            }
        }

        public void HighlightCell(GridCell cell, bool collapse)
        {            
            if (collapse)
                FullCollapse(cell);
            ExpandParentCell(cell);
            using (Graphics g = CreateGraphics())
                MakeCellVisible(g, cell, false);
            _focusedCell = cell;            
        }

        private GridCell GetRootCell(GridCell cell)
        {
            while (cell.Parent != _rootCell)
                cell = cell.Parent;
            return cell;
        }

        #endregion

        #region Service

        public GridCell FindCellByPoint(Point pt, ref Rectangle rcResult)
        {
            Rectangle cellRect = new Rectangle(0, 0, _drawInfo.iMaxWidth, _drawInfo.iHeight);
            return FindCellByPoint(_rootCell, pt, cellRect, ref rcResult);
        }

        private GridCell FindCellByPoint(GridCellGroup cell, Point pt, Rectangle cellRect, ref Rectangle rcResult)
        {
            if (cell.Expanded)
            {
                Rectangle rc = Rectangle.FromLTRB(
                   cellRect.Right - cell.TableWidth - cell.TablePadding,
                   cellRect.Bottom - cell.TableHeight,
                   cellRect.Right - cell.TablePadding,
                   cellRect.Bottom);
                if (rc.Contains(pt))
                {
                    int rcTop = rc.Top;
                    int s = -1;
                    for (int i = 0; i < cell.Table.Height; i++)
                    {
                        if (rcTop < pt.Y && pt.Y <= rcTop + cell.Table.RowHeight[i])
                        {
                            s = i;
                            break;
                        }
                        rcTop += cell.Table.RowHeight[i];
                    }
                    int rcLeft = rc.Left;
                    int k = -1;
                    if (s != -1)
                        for (int i = 0; i < cell.Table.Width; i++)
                        {
                            if (rcLeft < pt.X && pt.X <= rcLeft + cell.Table.ColumnsWidth[i])
                            {
                                k = i;
                                break;
                            }
                            rcLeft += cell.Table.ColumnsWidth[i];
                        }
                    if (k != -1 && s != -1)
                    {
                        rc = Rectangle.FromLTRB(rcLeft, rcTop, rcLeft + cell.Table.ColumnsWidth[k],
                            rcTop + cell.Table.RowHeight[s]);
                        if (cell.Table[k, s].IsGroup)
                            return FindCellByPoint((GridCellGroup)cell.Table[k, s], pt, rc, ref rcResult);
                        else
                        {
                            rcResult = rc;
                            return cell.Table[k, s];
                        }
                    }
                    else
                        return null;
                }
            }
            rcResult = cellRect;
            return cell;
        }

        public Rectangle FindCellRect(GridCell cell)
        {
            Rectangle cellRect = new Rectangle(0, 0, 
                _drawInfo.iMaxWidth, _drawInfo.iHeight);
            Rectangle rc = FindCellRect(_rootCell, cellRect, cell);
            return rc;
        }

        private Rectangle FindCellRect(GridCellGroup cell, Rectangle cellRect, GridCell dest)
        {
            if (cell.Expanded)
            {
                int rcLeft = cellRect.Right - cell.TableWidth - cell.TablePadding;
                for (int k = 0; k < cell.Table.Width; k++)
                {
                    int rcRight = rcLeft + cell.Table.ColumnsWidth[k];
                    int rcTop = cellRect.Bottom - cell.TableHeight;                    
                    for (int s = 0; s < cell.Table.Height; s++)
                    {
                        int rcBottom = rcTop + cell.Table.RowHeight[s];
                        Rectangle rc = Rectangle.FromLTRB(rcLeft, rcTop, rcRight, rcBottom);
                        if (cell.Table[k, s] == dest)
                            return rc;
                        else
                            if (cell.Table[k, s].IsGroup)
                            {
                                rc = FindCellRect((GridCellGroup)cell.Table[k, s], rc, dest);
                                if (!rc.IsEmpty)
                                    return rc;
                            }
                        rcTop = rcBottom;
                    }
                    rcLeft = rcRight;
                }
            }
            return Rectangle.Empty;
        }

        public Rectangle GetClientRect()
        {
            Rectangle rcClient = ClientRectangle;
            if (ShowColumnHeader)
            {
                rcClient.Y += _drawInfo.cyChar;
                rcClient.Height -= _drawInfo.cyChar;
            }
            if (_vScroll.Visible)
                rcClient.Width -= SystemInformation.VerticalScrollBarWidth;
            if (_hScroll.Visible)
                rcClient.Height -= SystemInformation.HorizontalScrollBarHeight;
            return rcClient;
        }

        public Rectangle GetWindowRect()
        {
            Rectangle rcClient = GetClientRect();
            int rcLeft = _drawInfo.cxChar * _drawInfo.xPos + rcClient.Left;
            int rcTop = _drawInfo.cyChar * _drawInfo.yPos + rcClient.Top;
            int rcRight = rcLeft + rcClient.Width;
            int rcBottom = rcTop + rcClient.Height;
            return Rectangle.FromLTRB(rcLeft, rcTop,
                rcRight, rcBottom);
        }

        public HitTest GetHitTest(Point p, out GridCell cell)
        {
            Rectangle cellRect = new Rectangle();
            cell = FindCellByPoint(p, ref cellRect);
            if (cell != null)
            {
                Rectangle rc;
                if (cell.ImageIndex != -1)
                {
                    rc = new Rectangle(cellRect.Left + 2, cellRect.Top + 2,
                        _images.ImageSize.Width, _images.ImageSize.Height);
                    if (rc.Contains(p))
                        return HitTest.Icon;
                }
                rc = cellRect;
                rc.X++;
                rc.Offset(0, 0);
                rc.Inflate(-3, 0);
                if (rc.Contains(p))
                    return HitTest.Text;
                else
                    return HitTest.Cell;
            }
            else
                return HitTest.Nothing;
        }

        protected bool IsBorder(int X)
        {
            int curr = 0;
            _columnIndex = -1;
            for (int i = 0; i < _columnsWidth.Length; i++)
            {
                curr += _columnsWidth[i];
                if (curr - 3 <= X && X <= curr + 2)
                {
                    _columnIndex = i;
                    return true;
                }
                else
                    if (curr > X)
                        break;
            }
            return false;
        }

        protected bool IsBorderLine(Point p)
        {
            if (p.Y <= _drawInfo.nLinesCount * _drawInfo.cyChar && IsBorder(p.X))
            {
                Rectangle cellRect = Rectangle.Empty;
                GridCell cell = FindCellByPoint(p, ref cellRect);
                if (cell == null || (cellRect.Right - 3 <= p.X && p.X <= cellRect.Right))
                    return true;
            }
            return false;
        }

        private void Expand(GridCellGroup cell, bool expand, GridCell stopCell)
        {
            for (int k = 0; k < cell.Table.Width; k++)
                for (int s = 0; s < cell.Table.Height; s++)
                    if (cell.Table[k, s].IsGroup && stopCell != cell.Table[k, s])
                    {
                        GridCellGroup child = (GridCellGroup)cell.Table[k, s];
                        if (!child.Overlaped)
                            if (expand)
                            {
                                child.BeforeExpand();
                                child.Flags = child.Flags | GroupFlags.Expanded;
                            }
                            else
                                child.Flags = child.Flags & ~GroupFlags.Expanded;
                        Expand(child, expand, stopCell);
                    }
        }

        public void Expand(GridCellGroup cell, bool recursive)
        {
            if (!cell.Overlaped)
            {
                cell.BeforeExpand();
                cell.Flags = cell.Flags | GroupFlags.Expanded;
                if (recursive)
                    Expand(cell, true, null);
                MeasureCells();
                UpdateScrollRange();
                Refresh();
            }
        }

        public void Expand(GridCellGroup cell)
        {
            if (! cell.Expanded)
                Expand(cell, false);
        }

        public void Collapse(GridCellGroup cell, bool recursive)
        {
            if (!cell.Overlaped)
            {
                cell.Flags = cell.Flags & ~GroupFlags.Expanded;
                if (recursive)
                    Expand(cell, false, null);
                MeasureCells();
                UpdateScrollRange();
                Refresh();
            }
        }

        public void Collapse(GridCellGroup cell)
        {
            if (cell.Expanded)
                Collapse(cell, false);
        }

        public void FullCollapse(GridCell branch)
        {
            Expand(_rootCell, false, branch);
            MeasureCells();
            UpdateScrollRange();
            Refresh();
        }

        public void FullExpand()
        {
            Expand(_rootCell, true, null);
            MeasureCells();
            UpdateScrollRange();
            Refresh();
        }

        public void ExpandParentCell(GridCell cell)
        {
            bool needMeasure = false;
            GridCellGroup parent = cell.Parent;
            while (parent != null)
            {
                if (!parent.Expanded)
                {
                    parent.Flags = parent.Flags | GroupFlags.Expanded;
                    needMeasure = true;
                }
                parent = parent.Parent;
            }
            if (needMeasure)
            {
                MeasureCells();
                UpdateScrollRange();
            }            
        }

        private int GetCellRight(Graphics g, GridCell cell, Rectangle cellRect, bool fullVisible)
        {
            if (fullVisible)
                return cellRect.Right;
            else
                return cellRect.Left + Math.Max(cell.GetTextWidth(this, g, Font, _drawInfo), _rowNumWidth);
        }

        private int GetCellBottom(Graphics g, GridCell cell, Rectangle cellRect, bool fullVisible)
        {
            if (fullVisible || !cell.IsGroup)
                return cellRect.Bottom;
            else
                return cellRect.Top + _drawInfo.cyChar;
        }

        public void MakeCellVisible(Graphics g, GridCell cell, bool fullVisible)
        {
            ExpandParentCell(cell);
            Rectangle rcClient = GetClientRect();
            Rectangle rcWindow = GetWindowRect();          
            Rectangle rc = FindCellRect(cell);
            if (rc.IsEmpty)
                return;
            int X = -1;
            if (rc.Left < rcWindow.Left || GetCellRight(g, cell, rc, fullVisible) > rcWindow.Right)
                if (rc.Width < rcClient.Right)
                {
                    X = 0;
                    for (int i = 0; i < _columnsWidth.Length; i++)
                    {
                        if (X <= rc.Left && rc.Right <= X + rcClient.Right)
                        {
                            int dist = Math.Abs(X - rcWindow.Left);
                            for (int k = i + 1; k < _columnsWidth.Length; k++)
                            {
                                int v = X + _columnsWidth[k];
                                if (!(v <= rc.Left && rc.Right <= v + rcClient.Right) ||
                                    (dist < Math.Abs(v - rcWindow.Left)))
                                    break;
                                X = v;
                                dist = Math.Abs(v - rcWindow.Left);
                            }
                            break;
                        }
                        X += _columnsWidth[i];
                    }
                }
                else
                    X = rc.Left;
            int Y = -1;
            if (GetCellBottom(g, cell, rc, fullVisible) > rcWindow.Bottom || rc.Top < rcWindow.Top)
                if (rc.Height < rcClient.Bottom)
                {
                    Y = 0;
                    for (int i = 0; i < _rowHeight.Length; i++)
                    {
                        if (Y <= rc.Top && rc.Bottom <= Y + rcClient.Bottom)
                        {
                            int dist = Math.Abs(Y - rcWindow.Top);
                            for (int s = i + 1; s < _rowHeight.Length; s++)
                            {
                                int v = Y + _rowHeight[s];
                                if (ShowColumnHeader)
                                {
                                    if (!(v < rc.Top && rc.Bottom <= v + rcClient.Height) ||
                                       (dist < Math.Abs(v - rcWindow.Top)))
                                        break;
                                }
                                else
                                {
                                    if (!(v <= rc.Top && rc.Bottom <= v + rcClient.Height) ||
                                           (dist < Math.Abs(v - rcWindow.Top)))
                                        break;
                                }
                                Y = v;
                                dist = Math.Abs(Y - rcWindow.Top);
                            }
                            break;
                        }
                        Y += _rowHeight[i];
                    }
                }
                else
                    if (rc.Top + _drawInfo.cyChar <= rcWindow.Top)
                        Y = rc.Top;
            if (X != -1)
                X = X / _drawInfo.cxChar;
            if (Y != -1)
                Y = Y / _drawInfo.cyChar;
            ScrollTo(X, Y);
        }       

        private void SetFocusedCell(GridCell cell)
        {
            GridCell target = null;
            if (cell != null && cell.Parent != null && 
                    cell.Index == 0 && cell is GridRowLabel)
                target = cell.Parent;
            else
                target = cell;
            if (target != _focusedCell)
            {
                if (target == _rootCell)
                    _focusedCell = null;
                else
                    _focusedCell = target;
                Invalidate();
            }            
        }
        
        #endregion

        #region Scroll
        private void UpdateScrollRange()
        {
            if (!IsHandleCreated)
                return;

            Size client = ClientSize;
            _drawInfo.nLinesPerPage = client.Height / _drawInfo.cyChar;
            _drawInfo.iPageWidth = client.Width / _drawInfo.cxChar;
            
            Size canvas = new Size(_drawInfo.iMaxWidth, 
                (_drawInfo.nLinesCount + _drawInfo.nFooterLines) * _drawInfo.cyChar);

            int right_edge = client.Width;
            int bottom_edge = client.Height;
            int prev_right_edge;
            int prev_bottom_edge;

            bool hscroll_visible;
            bool vscroll_visible;

            do
            {
                prev_right_edge = right_edge;
                prev_bottom_edge = bottom_edge;

                if (canvas.Width > right_edge && client.Width > 0)
                {
                    hscroll_visible = true;
                    bottom_edge = client.Height - SystemInformation.HorizontalScrollBarHeight;
                }
                else
                {
                    hscroll_visible = false;
                    bottom_edge = client.Height;
                }

                if (canvas.Height > bottom_edge && client.Height > 0)
                {
                    vscroll_visible = true;
                    right_edge = client.Width - SystemInformation.VerticalScrollBarWidth;
                }
                else
                {
                    vscroll_visible = false;
                    right_edge = client.Width;
                }

            } while (right_edge != prev_right_edge || bottom_edge != prev_bottom_edge);


            if (vscroll_visible)
            {
                _vScroll.Minimum = 0;
                _vScroll.Maximum = _drawInfo.nLinesCount + _drawInfo.nFooterLines;
                _vScroll.SmallChange = 1;
                _vScroll.LargeChange = _drawInfo.nLinesPerPage;
                _vScroll.Visible = true;
            }
            else
            {
                _vScroll.Value = 0;
                _vScroll.Visible = false;
            }

            if (hscroll_visible)
            {
                _hScroll.Minimum = 0;
                _hScroll.Maximum = _drawInfo.iMaxWidth / _drawInfo.cxChar -1;
                _hScroll.SmallChange = 1;
                _hScroll.LargeChange = _drawInfo.iPageWidth;
                _hScroll.Visible = true;
            }
            else
            {
                _hScroll.Value = 0;
                _hScroll.Visible = false;
            }
        }

        public void ScrollTo(int X, int Y)
        {
            if (X >= 0 && X <= _hScroll.Maximum)
                _hScroll.Value = X;
            if (Y >= 0 && Y <= _vScroll.Maximum)
                _vScroll.Value = Y;
        }        

        private void HandleScrollBar(object sender, EventArgs e)
        {
            int inc;
            if (sender == _hScroll && _hScroll.Visible)
            {
                inc = _drawInfo.xPos - _hScroll.Value;                
                Win32.ScrollWindowEx(Handle, _drawInfo.cxChar * inc, 0,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Win32.SW_INVALIDATE);
                _drawInfo.xPos = _hScroll.Value;                
            }
            else if (sender == _vScroll && _vScroll.Visible)
            {
                inc = _drawInfo.yPos - _vScroll.Value;
                Win32.RECT rcClient = new Win32.RECT(GetClientRect());
                IntPtr pRect = Marshal.AllocHGlobal(Marshal.SizeOf(rcClient));
                Marshal.StructureToPtr(rcClient, pRect, true);
                Win32.ScrollWindowEx(Handle, 0, _drawInfo.cyChar * inc,
                    pRect, pRect, IntPtr.Zero, IntPtr.Zero, Win32.SW_INVALIDATE);
                Marshal.FreeHGlobal(pRect);
                _drawInfo.yPos = _vScroll.Value;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            UpdateScrollRange();
        }

        #endregion

        #region Mouse

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_rootCell == null)
            {
                Focus();
                return;
            }
            Point p = new Point();
            p.X = e.X + _drawInfo.xPos * _drawInfo.cxChar;
            p.Y = e.Y + _drawInfo.yPos * _drawInfo.cyChar;
            if (e.Button == MouseButtons.Left && IsBorderLine(p))
            {
                _resizeFlag = true;
                Cursor.Current = Cursors.VSplit;
                Refresh();
            }
            else
            {
                Focus();
                GridCell cell;
                HitTest hitTest = GetHitTest(p, out cell);
                if (e.Button == MouseButtons.Left &&
                    hitTest == HitTest.Icon && cell.IsGroup)
                {
                    GridCellGroup group = (GridCellGroup)cell;
                    if (!group.Expanded)
                        Expand(group, Control.ModifierKeys == Keys.Shift);
                    else
                        Collapse(group, Control.ModifierKeys == Keys.Shift);
                    if (group.Expanded)
                        using (Graphics g = CreateGraphics())
                            MakeCellVisible(g, cell, false);
                }
                else
                    if (hitTest != HitTest.Nothing)
                    {
                        if (e.Button == MouseButtons.Left && cell != _focusedCell)
                            SetFocusedCell(cell);
                    }
                    else
                        SetFocusedCell(null);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizeFlag)
            {
                _resizeFlag = false;
                Refresh();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_rootCell == null)
                return;
            Point p = new Point();
            p.X = e.X + _drawInfo.xPos * _drawInfo.cxChar;
            p.Y = e.Y + _drawInfo.yPos * _drawInfo.cyChar;
            if (!_resizeFlag)
            {
                if (IsBorderLine(p))
                    Cursor.Current = Cursors.VSplit;
                else
                    Cursor.Current = Cursors.Default;
            }
            else
            {
                int delta = p.X - RangeWidth(0, _columnIndex);
                if (delta > _rowNumWidth + 5)
                {
                    _columnsWidth[_columnIndex] = delta;
                    UpdateWidth();
                    if (AutoHeightCells)
                        using (Graphics g = CreateGraphics())
                            UpdateHeight(g);
                    UpdateScrollRange();
                    Refresh();                    
                }
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (_rootCell == null)
                return;
            if (_resizeFlag)
            {
                int iMaxWidth = _columnsWidth[_columnIndex];
                using (Graphics g = CreateGraphics())
                    for (int s = 0; s < _rootCell.Table.Height; s++)
                    {
                        GridCell cell = _rootCell.Table[_columnIndex, s];
                        if (!cell.IsGroup)
                            iMaxWidth = Math.Max(iMaxWidth, 2 * _drawInfo.cxChar + 
                                cell.GetTextWidth(this, g, Font, _drawInfo));
                    }
                _columnsWidth[_columnIndex] = iMaxWidth;
                UpdateWidth();
                if (AutoHeightCells)
                    using (Graphics g = CreateGraphics())
                        UpdateHeight(g);
                UpdateScrollRange();
                Refresh();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_rootCell == null)
                return;
            int newPos = _drawInfo.yPos - e.Delta / _drawInfo.cyChar;
            if (newPos < 0)
                newPos = 0;
            else
                if (newPos > _drawInfo.nLinesCount - _drawInfo.nLinesPerPage + 1)
                    newPos = _drawInfo.nLinesCount - _drawInfo.nLinesPerPage + 1;
            if (newPos >= 0)
                ScrollTo(-1, newPos);
        }

        #endregion

        #region Keyborad       

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.Left || keyData == Keys.Right ||
                keyData == Keys.PageUp || keyData == Keys.PageDown ||
                keyData == Keys.Tab || keyData == Keys.Home || 
                keyData == Keys.End) {
                return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            GridCell cell;
            Rectangle rc;
            base.OnKeyDown(e);
            if (_focusedCell == null)
                return;
            switch (e.KeyCode)
            {
                case Keys.Up:
                    cell = PriorCell(_focusedCell);
                    if (cell != null)
                        if (e.Control)
                        {
                            rc = FindCellRect(cell);
                            if (!rc.IsEmpty)
                            {
                                Rectangle window = GetWindowRect();
                                ScrollTo(-1, (window.Top - rc.Height -1) / _drawInfo.cyChar);
                            }
                        }
                        else
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    break;

                case Keys.Down:
                    cell = NextCell(_focusedCell, true);
                    if (cell != null)
                        if (e.Control)
                        {
                            rc = FindCellRect(cell);
                            if (!rc.IsEmpty)
                            {
                                Rectangle window = GetWindowRect();
                                ScrollTo(-1, (window.Top + rc.Height) / _drawInfo.cyChar);
                            }
                        }
                        else
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    break;

                case Keys.Left:
                    cell = BackwardCell(_focusedCell);
                    if (cell != null)
                        if (e.Control)
                        {
                            rc = FindCellRect(cell);
                            if (!rc.IsEmpty)
                            {
                                Rectangle window = GetWindowRect();
                                ScrollTo((window.Left - rc.Width) / _drawInfo.cxChar, -1);
                            }
                        }
                        else
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    break;

                case Keys.Right:
                    cell = ForwardCell(_focusedCell);
                    if (cell != null)
                        if (e.Control)
                        {
                            rc = FindCellRect(cell);
                            if (!rc.IsEmpty)
                            {
                                Rectangle window = GetWindowRect();
                                ScrollTo((window.Left + rc.Width) / _drawInfo.cxChar, -1);
                            }
                        }
                        else
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    break;

                case Keys.Tab:
                    if (_focusedCell.Parent != null && _focusedCell.Parent.TableView)
                    {
                        cell = null;
                        if (_focusedCell.Col < _focusedCell.Parent.Table.Width - 1)
                            cell = ForwardCell(_focusedCell);
                        else
                            if (_focusedCell.Row < _focusedCell.Parent.Table.Height - 1)
                                cell = _focusedCell.Parent.Table[1, _focusedCell.Row + 1];
                        if (cell != null)
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    }
                    else
                    {
                        cell = NextCell(_focusedCell, true);
                        if (cell != null)
                        {
                            SetFocusedCell(cell);
                            using (Graphics g = CreateGraphics())
                                MakeCellVisible(g, cell, false);
                        }
                    }
                    break;

                case Keys.Home:
                    cell = _focusedCell.Parent.Table[0, 0];
                    SetFocusedCell(cell);
                    using (Graphics g = CreateGraphics())
                        MakeCellVisible(g, cell, false);
                    break;

                case Keys.End:
                    cell = _focusedCell.Parent.Table[_focusedCell.Parent.Table.Width - 1, 
                        _focusedCell.Parent.Table.Height - 1];
                    SetFocusedCell(cell);
                    using (Graphics g = CreateGraphics())
                        MakeCellVisible(g, cell, false);
                    break;

                case Keys.C:
                    if (e.Control)
                        if (_focusedCell != null)
                            _focusedCell.CopyToClipboard();
                    break;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (_focusedCell != null)
            {
                switch (e.KeyChar)
                {
                    case '+':
                        if (_focusedCell.IsGroup)
                            Expand((GridCellGroup)_focusedCell);
                        break;

                    case '-':
                        if (_focusedCell.IsGroup)
                            Collapse((GridCellGroup)_focusedCell);
                        break;
                }
            }
        }

        #endregion

        #region Paint

        protected virtual void DrawCell(Graphics g, int X, int Y, GridCell cell,
            int pixelWidth, int pixelHeight, bool isSelected)
        {
            Rectangle cellRect = Rectangle.FromLTRB(X + 1, Y + 1,
                X + pixelWidth, Y + pixelHeight);            
            Brush brush;
            if (cell is GridHeadLabel)
            {
                brush = new LinearGradientBrush(cellRect, Color.White, 
                    SystemColors.ButtonFace, LinearGradientMode.Vertical); 
            }
            else
            {
                Color color;
                if (cell == _focusedCell)
                {
                    if (Focused)
                        color = Color.FromArgb(0xA6, 0xCA, 0xF0);
                    else
                        color = SystemColors.InactiveCaption;
                }
                else
                    if (isSelected)
                        if (cell is GridRowLabel || cell is GridColumnLabel)
                            color = Color.FromArgb(0x00, 0x00, 0xFF);
                        else
                            if (Focused)
                                color = Color.FromArgb(0x31, 0x6A, 0xC5);
                            else
                                if (String.IsNullOrEmpty(cell.Text))
                                    color = Color.White;
                                else
                                    color = Color.FromArgb(0xF4, 0xF7, 0xFC);
                    else
                        if (cell is GridRowLabel || cell is GridColumnLabel)
                            color = Color.FromArgb(0xFF, 0xFF, 0xC0);
                        else
                            if (String.IsNullOrEmpty(cell.Text))
                                color = Color.FromArgb(0xF0, 0xF0, 0xF0);
                            else
                                color = BackColor;
                brush = new SolidBrush(color);
            }
            Brush textBrush = new SolidBrush(ForeColor);
            g.FillRectangle(brush, cellRect);
            if (cell == _focusedCell)
                ControlPaint.DrawFocusRectangle(g, cellRect);
            if (cell.ImageIndex != -1)
            {                                    
                if (cell.ImageIndex <= 1)
                    g.DrawImage(_images.Images[cell.ImageIndex], X + 5, Y + 3);
                else
                    g.DrawImage(_images.Images[cell.ImageIndex], X + 2, Y + 2);
                cellRect.X += _images.ImageSize.Width +1;
                cellRect.Width -= _images.ImageSize.Width + 1;
            }
            cellRect.Offset(0, -1);
            cellRect.Inflate(-3, -1);
            StringFormat sf = cell.GetStringFormat();            
            if (cell is GridRowLabel || cell is GridColumnLabel)
            {
                Font font = new Font(Font, FontStyle.Bold);
                if (cell is GridRowLabel)
                    sf.Alignment = StringAlignment.Far;
                g.DrawString(cell.Text, font, textBrush, cellRect, sf);
            }
            else
                if (cell.Text != null)
                    cell.DrawCellText(this, g, Font, textBrush, sf, _drawInfo, cellRect);
        }

        protected virtual void DrawGridTable(Graphics g, RectangleF clipRect, int X, int Y, GridCellTable table,
            int pixelWidth, int pixelHeight, bool isSelected)
        {
            Pen pen = new Pen(Color.Silver);
            int left = X;
            int bottom = Y + pixelHeight;
            for (int i = 0; i < table.Width - 1; i++)
            {
                left += table.ColumnsWidth[i];
                g.DrawLine(pen, left, Y, left, bottom);
            }
            int top = Y;
            int right = X + pixelWidth;
            for (int i = 0; i < table.Height - 1; i++)
            {
                top += table.RowHeight[i];
                g.DrawLine(pen, X, top, right, top);
            }
            top = Y;
            for (int s = 0; s < table.Height; s++)
            {
                left = X;
                int H = table.RowHeight[s];
                if (clipRect.Top < top + H && top <= clipRect.Bottom)
                    for (int k = 0; k < table.Width; k++)
                    {
                        int W = table.ColumnsWidth[k];
                        if (clipRect.Left < left + W && left <= clipRect.Right)
                        {
                            GridCell cell = table[k, s];
                            bool cellSelected = isSelected || 
                                (table[k, 0] is GridColumnLabel && table[k, 0] == _focusedCell) ||
                                    (table[0, s] is GridRowLabel && table[0, s] == _focusedCell);
                            if (cell.IsGroup)
                            {
                                GridCellGroup groupCell = (GridCellGroup)cell;
                                if (groupCell.Overlaped)
                                    DrawGridTable(g, clipRect, left, top, groupCell.Table, W, H, cellSelected);
                                else
                                    DrawGroupCell(g, clipRect, left, top, groupCell, W, H, cellSelected);
                            }
                            else
                                DrawCell(g, left, top, cell, W, H, cellSelected);
                        }
                        left += W;
                    }
                top += H;
            }
            if (top < Y + pixelHeight)
                g.DrawLine(pen, X, top, X + pixelWidth, top);
        }

        protected virtual void DrawGroupCell(Graphics g, RectangleF clipRect, int X, int Y, GridCellGroup cell,
            int pixelWidth, int pixelHeight, bool isSelected)
        {
            DrawCell(g, X, Y, cell, pixelWidth, pixelHeight, isSelected);
            if (cell.Expanded)
            {
                int left = pixelWidth - cell.TableWidth - cell.TablePadding;
                int top = pixelHeight - cell.TableHeight;
                Pen pen = new Pen(Color.Silver);
                g.DrawLine(pen, X + left, Y + top, X + left, Y + pixelHeight);
                g.DrawLine(pen, X + left, Y + top, X + pixelWidth, Y + top);
                if (cell == _focusedCell)
                {
                    Rectangle rc = Rectangle.FromLTRB(X + left - 1, Y + top - 1, 
                        X + pixelWidth, Y + pixelHeight);
                    ControlPaint.DrawFocusRectangle(g, rc);
                    Pen framePen = new Pen(Color.FromArgb(0xA6, 0xCA, 0xF0));
                    g.DrawLine(framePen, X + pixelWidth - 1, Y + top, 
                        X + pixelWidth - 1, Y + pixelHeight);
                    g.DrawLine(framePen, X + pixelWidth - cell.TablePadding - 1, Y + pixelHeight - 1,
                        X + pixelWidth, Y + pixelHeight - 1);
                }
                if (cell.TablePadding > 0)
                    g.DrawLine(pen, X + pixelWidth - cell.TablePadding, Y + Top,
                        X + pixelWidth - cell.TablePadding, Y + pixelHeight);
                DrawGridTable(g, clipRect, X + left, Y + top, cell.Table, cell.TableWidth, cell.TableHeight,
                    isSelected || cell == _focusedCell);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            //g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.TranslateTransform(-_drawInfo.cxChar * _drawInfo.xPos,
                -_drawInfo.cyChar * _drawInfo.yPos);
            RectangleF clipRect = g.ClipBounds;
            if (ShowColumnHeader)
            {
                Rectangle window = GetWindowRect();
                RectangleF rc = RectangleF.FromLTRB(window.Left, window.Top, 
                    window.Right, window.Bottom);
                clipRect.Intersect(rc);
            }            
            if (_rootCell != null)
            {                
                DrawGridTable(g, clipRect, 0, 0, 
                    _rootCell.Table, _drawInfo.iMaxWidth, _drawInfo.iHeight, false);
                if (ShowColumnHeader)
                {
                    int top = _drawInfo.cyChar * _drawInfo.yPos;
                    int H = _rootCell.Table.RowHeight[0];
                    if (g.ClipBounds.Top < top + H && top <= g.ClipBounds.Bottom)
                    {
                        int left = 0;
                        for (int k = 0; k < _rootCell.Table.Width; k++)
                        {
                            int W = _rootCell.Table.ColumnsWidth[k];
                            if (g.ClipBounds.Left < left + W && left <= g.ClipBounds.Right)
                                DrawCell(g, left, top, _rootCell.Table[k, 0], W, H, false);
                            left += W;
                        }
                    }
                }
                Pen pen = new Pen(Color.Silver); 
                g.DrawLine(pen, 0, 0, 0, _drawInfo.iHeight);
                if (ShowColumnHeader)
                    g.DrawLine(pen, 0, _drawInfo.cyChar * _drawInfo.yPos,
                        _drawInfo.iMaxWidth, _drawInfo.cyChar * _drawInfo.yPos);
                else
                    g.DrawLine(pen, 0, 0, _drawInfo.iMaxWidth, 0);
                g.DrawLine(pen, 0, _drawInfo.iHeight, _drawInfo.iMaxWidth, _drawInfo.iHeight);
                g.DrawLine(pen, _drawInfo.iMaxWidth, 0, _drawInfo.iMaxWidth, _drawInfo.iHeight);
            }
            if (Footer != null)
            {                
                Font f;
                int top;
                if (_rootCell != null)
                {
                    f = new Font(Font, FontStyle.Italic);
                    top = _drawInfo.iHeight + _drawInfo.cyChar / 2;
                }
                else
                {
                    f = Font;
                    top = 0;
                }
                Brush brush = new SolidBrush(SystemColors.WindowText);                
                foreach (string s in Footer)
                {
                    g.DrawString(s, f, brush, _drawInfo.cxChar, top);
                    top += _drawInfo.cyChar;
                }
            }
            if (_resizeFlag)
            {
                Pen pen = new Pen(Color.Black);
                int H = RangeWidth(0, _columnIndex + 1);
                g.DrawLine(pen, H, e.ClipRectangle.Top, 
                    H, e.ClipRectangle.Bottom);
            }
        }

        #endregion
    }
}
