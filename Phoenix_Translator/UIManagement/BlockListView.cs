using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace PhoenixTranslator.UIManagement
{
    public class BlockListView
    {
        public List<ExColStyle> ExColStyles = new List<ExColStyle>();

        private Grid Parent = null;
        private ScrollViewer Scroll = null;
        private Grid MainGrid;

        public List<ExRow> Rows = new List<ExRow>();
        public double LineHeight = 0;

        public BlockListView(Grid Parent, double LineHeight)
        {
            Style ScrollBarStyle = new Style(typeof(ScrollBar))
            {
                BasedOn = (Style)Application.Current.FindResource("for_scrollbar")
            };
            Parent.Resources.Add(typeof(ScrollBar), ScrollBarStyle);

            this.LineHeight = LineHeight;

            this.MainGrid = new Grid();
            this.MainGrid.Background = null;

            ScrollViewer OneScroll = new ScrollViewer();
            OneScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            OneScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            OneScroll.Content = MainGrid;

            this.Scroll = OneScroll;

            Parent.Children.Add(OneScroll);
            this.Parent = Parent;
        }

        public void Clear()
        {
            this.Rows.Clear();
            this.MainGrid.Children.Clear();

            this.MainGrid.RowDefinitions.Clear();
            this.MainGrid.ColumnDefinitions.Clear();

            this.Scroll.Content = this.MainGrid;

            this.Scroll.ScrollToTop();
        }

        public Grid GetMainGrid()
        {
            if (this.Scroll.Content != null)
            {
                return this.Scroll.Content as Grid;
            }

            return null;
        }

        private void Refresh()
        {
            if (this.Rows.Count == 0) return;

            var GetGrid = GetMainGrid();

            if (GetGrid != null)
            {
                this.MainGrid.Children.Clear();
                this.MainGrid.ColumnDefinitions.Clear();
                this.MainGrid.RowDefinitions.Clear();

                foreach (var GetStyles in this.ExColStyles)
                {
                    ColumnDefinition OneColumn = new ColumnDefinition();
                    OneColumn.Width = new GridLength(GetStyles.Width, GetStyles.UnitType);
                    this.MainGrid.ColumnDefinitions.Add(OneColumn);
                }

                int RowOffset = 0;
                int ColOffset = 0;

                foreach (var GetControlRow in this.Rows)
                {
                    ColOffset = 0;

                    foreach (var GetCol in GetControlRow.Columns)
                    {
                        this.MainGrid.Children.Add(GetCol.Control);
                        Grid.SetColumn(GetCol.Control, ColOffset);
                        Grid.SetRow(GetCol.Control, RowOffset);

                        ColOffset++;
                    }

                    RowDefinition OneRow = new RowDefinition();
                    OneRow.Height = new GridLength(this.LineHeight, GridUnitType.Pixel);
                    this.MainGrid.RowDefinitions.Add(OneRow);
                    RowOffset++;
                }
            }

        }

        public List<string> AddBlock(params UIElement[] Controls)
        {
            List<string> Names = new List<string>();

            ExRow OneRow = null;

            int WaitAutoReturnCount = 0;

            if (this.Rows.Count == 0)
            {
                OneRow = new ExRow(this.Rows.Count + 1);
                this.Rows.Add(OneRow);
                WaitAutoReturnCount = this.ExColStyles.Count;
            }
            else
            {
                if (this.Rows[this.Rows.Count - 1].Columns.Count < this.ExColStyles.Count)
                {
                    WaitAutoReturnCount = this.ExColStyles.Count - this.Rows[this.Rows.Count - 1].Columns.Count;
                    OneRow = this.Rows[this.Rows.Count - 1];
                }
                else
                {
                    OneRow = new ExRow(this.Rows.Count + 1);
                    this.Rows.Add(OneRow);
                    WaitAutoReturnCount = this.ExColStyles.Count;
                }
            }

            int ColOffset = 0;

            foreach (var Get in ExColStyles)
            {
                string CreatControlName = "R{0}C{1}";

                if (Controls.Length < ColOffset + 1) break;

                if (WaitAutoReturnCount > 0)
                {
                    CreatControlName = string.Format(CreatControlName, this.Rows.Count, ColOffset);

                    Names.Add(CreatControlName);
                    OneRow.Columns.Add(new ExCol(Controls[ColOffset], CreatControlName));

                    WaitAutoReturnCount--;
                }
                else
                {
                    OneRow = new ExRow(this.Rows.Count + 1);
                    ColOffset = -1;
                    this.Rows.Add(OneRow);
                    CreatControlName = string.Format(CreatControlName, this.Rows.Count, ColOffset);
                    Names.Add(CreatControlName);
                    OneRow.Columns.Add(new ExCol(Controls[ColOffset], CreatControlName));
                    WaitAutoReturnCount = this.ExColStyles.Count - 1;
                }

                ColOffset++;
            }

            Refresh();

            return Names;
        }

        /// <summary>
        /// return Offset
        /// </summary>
        /// <param name="Controls"></param>
        /// <returns></returns>
        public int AddRow(params UIElement[] Controls)
        {
            if (Controls.Length != this.ExColStyles.Count) return -1;

            ExRow OneRow = null;
            OneRow = new ExRow(this.Rows.Count + 1);
            this.Rows.Add(OneRow);

            int ColOffset = 0;

            foreach (var Get in ExColStyles)
            {
                string CreatControlNameA = string.Format("R{0}C{1}", this.Rows.Count, ColOffset);

                OneRow.Columns.Add(new ExCol(Controls[ColOffset], CreatControlNameA));

                ColOffset++;
            }

            int RowSign = this.Rows.Count - 1;

            if (this.MainGrid.ColumnDefinitions.Count == 0)
            {
                foreach (var GetStyles in this.ExColStyles)
                {
                    ColumnDefinition OneColumn = new ColumnDefinition();
                    OneColumn.Width = new GridLength(GetStyles.Width, GetStyles.UnitType);
                    this.MainGrid.ColumnDefinitions.Add(OneColumn);
                }
            }

            RowDefinition OneGridRow = new RowDefinition();
            OneGridRow.Height = new GridLength(this.LineHeight, GridUnitType.Pixel);
            this.MainGrid.RowDefinitions.Add(OneGridRow);

            int GridColOffset = 0;

            foreach (var GetCol in OneRow.Columns)
            {
                this.MainGrid.Children.Add(GetCol.Control);
                Grid.SetColumn(GetCol.Control, GridColOffset);
                Grid.SetRow(GetCol.Control, RowSign);

                GridColOffset++;
            }

            return this.Rows.Count - 1;
        }

        public void DeleteRow(int Offset)
        {
            if (this.Rows.Count > (Offset + 1))
            {
                this.Rows.RemoveAt(Offset);
                Refresh();
            }
        }

        public void DeleteByName(string Name)
        {
            for (int i = 0; i < this.Rows.Count; i++)
            {
                for (int ir = 0; ir < this.Rows[i].Columns.Count; ir++)
                {
                    if (this.Rows[i].Columns[ir].ControlName.Equals(Name))
                    {
                        this.Rows[i].Columns.RemoveAt(ir);
                    }
                }
            }

            Refresh();
        }

        public void MoveToEnd()
        {
            this.Scroll.ScrollToEnd();
        }
    }

    public class ExColStyle
    {
        public double Width = 0;
        public GridUnitType UnitType;

        public ExColStyle(double Width = 1, GridUnitType UnitType = GridUnitType.Star)
        {
            this.Width = Width;
            this.UnitType = UnitType;
        }
    }
    public class ExCol
    {
        public UIElement Control = null;
        public string ControlName;

        public ExCol(UIElement Control, string ControlName)
        {
            this.Control = Control;
            this.ControlName = ControlName;
        }
    }
    public class ExRow : IComparable<ExRow>
    {
        public int ID = 0;

        public int TopIndex = 0;

        public List<ExCol> Columns = new List<ExCol>();

        public int CompareTo(ExRow p)
        {
            if (this.TopIndex > p.TopIndex)
                return 1;
            else
                return -1;
        }

        public ExRow(int ID)
        {
            this.ID = ID;
            this.TopIndex = ID;
        }
    }
}
