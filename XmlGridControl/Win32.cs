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
using System.Runtime.InteropServices;

namespace WmHelp.XmlGrid
{
    internal class Win32
    {
        [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct TEXTMETRIC
        {
            public int tmHeight;
            public int tmAscent;
            public int tmDescent;
            public int tmInternalLeading;
            public int tmExternalLeading;
            public int tmAveCharWidth;
            public int tmMaxCharWidth;
            public int tmWeight;
            public int tmOverhang;
            public int tmDigitizedAspectX;
            public int tmDigitizedAspectY;
            public byte tmFirstChar;
            public byte tmLastChar;
            public byte tmDefaultChar;
            public byte tmBreakChar;
            public byte tmItalic;
            public byte tmUnderlined;
            public byte tmStruckOut;
            public byte tmPitchAndFamily;
            public byte tmCharSet;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetOutlineTextMetrics(IntPtr hdc, uint strSize, IntPtr lptm);

        public static TEXTMETRIC GetTextMetrics(IntPtr hdc)
        {
            TEXTMETRIC lptm;
            bool rc = GetTextMetrics(hdc, out lptm);
            return lptm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ScrollInfoStruct
        {
            public int cbSize;
            public int fMask;
            public int nMin;
            public int nMax;
            public int nPage;
            public int nPos;
            public int nTrackPos;
        }

        public const int SBM_SETSCROLLINFO = 0x00E9;
        public const int WM_MOUSEWHEEL = 0x20A;
        
        public const int SIF_TRACKPOS = 0x10;
        public const int SIF_RANGE = 0x1;
        public const int SIF_POS = 0x4;
        public const int SIF_PAGE = 0x2;
        public const int SIF_ALL = SIF_RANGE | SIF_PAGE | SIF_POS | SIF_TRACKPOS;

        public const int WS_VSCROLL = 0x200000;
        public const int WS_HSCROLL = 0x100000;

        public const int SB_HORZ = 0;
        public const int SB_VERT = 1;

        public class SB
        {
            public const int 
                 SB_LINEUP = 0,
                 SB_LINELEFT = 0,
                 SB_LINEDOWN = 1,
                 SB_LINERIGHT = 1,
                 SB_PAGEUP = 2,
                 SB_PAGELEFT = 2,
                 SB_PAGEDOWN = 3,
                 SB_PAGERIGHT = 3,
                 SB_THUMBPOSITION = 4,
                 SB_TOP = 6,
                 SB_LEFT = 6,
                 SB_BOTTOM = 7,
                 SB_RIGHT = 7,
                 SB_ENDSCROLL = 8,
                 SB_THUMBTRACK = 5;
        }

        public const int SW_SCROLLCHILDREN = 1;  
        public const int SW_INVALIDATE = 2;      
        public const int SW_ERASE = 4;
        public const int SW_SMOOTHSCROLL = 0x10;   

        public const int WM_HSCROLL = 0x0114;
        public const int WM_VSCROLL = 0x0115;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetScrollInfo(IntPtr hWnd, int n,
            ref ScrollInfoStruct lpScrollInfo);
        
        [DllImport("user32.dll")]
        public static extern int SetScrollInfo(IntPtr hWnd, int n,
            [MarshalAs(UnmanagedType.Struct)] ref ScrollInfoStruct lpcScrollInfo,
            bool b);

        [DllImport("user32.dll")]
        public static extern int GetScrollPos(IntPtr hWnd, int n);

        [DllImport("user32.dll")]
        public static extern int SetScrollPos(IntPtr hWnd, int n,
            int nPos, bool redraw);        

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetScrollRange(IntPtr hWnd, int n, 
            int nMinPos, int nMaxPos, bool redraw);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool ScrollWindowEx(IntPtr hWnd, int nXAmount, int nYAmount, 
            RECT rectScrollRegion, ref RECT rectClip, IntPtr hrgnUpdate, ref RECT prcUpdate, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern bool ScrollWindowEx(IntPtr hWnd, int nXAmount, int nYAmount, IntPtr rectScrollRegion,
            IntPtr rectClip, IntPtr hrgnUpdate, IntPtr prcUpdate, int flags);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiObj);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public class TRACKMOUSEEVENT
        {
            public int cbSize;
            public int dwFlags;
            public IntPtr hwndTrack;
            public int dwHoverTime = 0;
            public TRACKMOUSEEVENT()
            {
                this.cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT));
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct NCCALCSIZE_PARAMS
        {
            public RECT rgrc0, rgrc1, rgrc2;
            public IntPtr lppos;
            public static NCCALCSIZE_PARAMS GetFrom(IntPtr lParam)
            {
                return (NCCALCSIZE_PARAMS)Marshal.PtrToStructure(lParam, typeof(NCCALCSIZE_PARAMS));
            }
            public void SetTo(IntPtr lParam)
            {
                Marshal.StructureToPtr(this, lParam, false);
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            public int reserved1;
            public int reserved2;
            public int reserved3;
            public int reserved4;
            public int reserved5;
            public int reserved6;
            public int reserved7;
            public int reserved8;
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
            public Point ToPoint() { return new Point(X, Y); }
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SIZE
        {
            public int Width, Height;
            public Size ToSize() { return new Size(Width, Height); }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
            public RECT(int l, int t, int r, int b)
            {
                Left = l; Top = t; Right = r; Bottom = b;
            }
            public RECT(Rectangle r)
            {
                Left = r.Left; Top = r.Top; Right = r.Right; Bottom = r.Bottom;
            }
            public Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved,
            ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
            public static MINMAXINFO GetFrom(IntPtr lParam)
            {
                return (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
            }
        }

        public class WMSZ
        {
            public const int
                WMSZ_LEFT = 1,
                WMSZ_RIGHT = 2,
                WMSZ_TOP = 3,
                WMSZ_TOPLEFT = 4,
                WMSZ_TOPRIGHT = 5,
                WMSZ_BOTTOM = 6,
                WMSZ_BOTTOMLEFT = 7,
                WMSZ_BOTTOMRIGHT = 8;
        }

        public class SWP
        {
            public const int
                SWP_NOSIZE = 0x0001,
                SWP_NOMOVE = 0x0002,
                SWP_NOZORDER = 0x0004,
                SWP_NOREDRAW = 0x0008,
                SWP_NOACTIVATE = 0x0010,
                SWP_FRAMECHANGED = 0x0020,
                SWP_DRAWFRAME = SWP_FRAMECHANGED,
                SWP_SHOWWINDOW = 0x0040,
                SWP_HIDEWINDOW = 0x0080,
                SWP_NOCOPYBITS = 0x0100,
                SWP_NOOWNERZORDER = 0x0200,
                SWP_NOREPOSITION = SWP_NOOWNERZORDER,
                SWP_NOSENDCHANGING = 0x0400;
        }

        public class DC
        {
            public const int
                DCX_WINDOW = 0x00000001,
                DCX_CACHE = 0x00000002,
                DCX_NORESETATTRS = 0x00000004,
                DCX_CLIPCHILDREN = 0x00000008,
                DCX_CLIPSIBLINGS = 0x00000010,
                DCX_PARENTCLIP = 0x00000020,
                DCX_EXCLUDERGN = 0x00000040,
                DCX_INTERSECTRGN = 0x00000080,
                DCX_EXCLUDEUPDATE = 0x00000100,
                DCX_INTERSECTUPDATE = 0x00000200,
                DCX_LOCKWINDOWUPDATE = 0x00000400,
                DCX_VALIDATE = 0x00200000;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hWnd;
            public IntPtr hHndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int flags;
        }

        public class SC
        {
            public const int
                SC_SIZE = 0xf000,
                SC_MOVE = 0xf010,
                SC_MINIMIZE = 0xf020,
                SC_MAXIMIZE = 0xf030,
                SC_NEXTWINDOW = 0xf040,
                SC_PREVWINDOW = 0xf050,
                SC_CLOSE = 0xf060,
                SC_VSCROLL = 0xf070,
                SC_HSCROLL = 0xf080,
                SC_MOUSEMENU = 0xf090,
                SC_KEYMENU = 0xf100,
                SC_ARRANGE = 0xf110,
                SC_RESTORE = 0xf120,
                SC_TASKLIST = 0xf130,
                SC_SCREENSAVE = 0xf140,
                SC_HOTKEY = 0xf150,
                SC_CONTEXTHELP = 0xf180,
                SC_DRAGMOVE = 0xf012,
                SC_SYSMENU = 0xf093;
        }

        public class HT
        {
            public const int HTERROR = (-2);
            public const int HTTRANSPARENT = (-1);
            public const int HTNOWHERE = 0, HTCLIENT = 1, HTCAPTION = 2, HTSYSMENU = 3,
                HTGROWBOX = 4, HTSIZE = HTGROWBOX, HTMENU = 5, HTHSCROLL = 6, HTVSCROLL = 7, HTMINBUTTON = 8, HTMAXBUTTON = 9,
                HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16,
                HTBOTTOMRIGHT = 17, HTBORDER = 18, HTREDUCE = HTMINBUTTON, HTZOOM = HTMAXBUTTON, HTSIZEFIRST = HTLEFT,
                HTSIZELAST = HTBOTTOMRIGHT, HTOBJECT = 19, HTCLOSE = 20, HTHELP = 21;
        }
    }
}
