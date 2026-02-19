using MathNet.Numerics.LinearAlgebra.Factorization;
using Microsoft.AspNetCore.Components;
using Newtonsoft.Json.Linq;
using System.Data.Common;
using System.Xml.Linq;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ProfileUI.ProfileEditor
{
    public class ProfileCore
    {
        private readonly string _tableName = "editordata";
        public enum GridColumn { Col1, Col2, Col3 }
        public List<CustomeGridRow> Data = new();
        public ServerDB _db;
        public ProfileCore(ServerDB db)
        {
            Data = new List<CustomeGridRow>();
            Data.Add(new CustomeGridRow
            {
                Col1 = -1,
                Col2 = -1,
                Col3 = -1,
                IsNewLine = true,
                IsSelected = true,
                RowId = Guid.NewGuid().ToString()
            });
            _db = db;
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public void RowUpdated(CustomeGridRow row)
        {
            int index = Data.IndexOf(row);
        }
        public void Add(CustomeGridRow row)
        {
            Data.Add(row);
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public void Remove(CustomeGridRow row)
        {
            Data.Remove(row);
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public void ClearData()
        {
            Data.Clear();
            Data.Add(new CustomeGridRow
            {
                Col1 = -1,
                Col2 = -1,
                Col3 = -1,
                IsNewLine = true,
                IsSelected = true,
                RowId = Guid.NewGuid().ToString()
            });
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public void RemoveRange(int start, int end)
        {
            Data.RemoveRange(start, end);
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public bool SortData(CustomeGridRow row)
        {
            int currentIndex = Data.IndexOf(row);
            Data = Data.OrderBy(r => r.IsNewLine ? decimal.MaxValue : r.Col1).ToList();
            if (currentIndex == Data.IndexOf(row))
                return false;
            else
            {
                _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
                return true;
            }
        }
        public void RecalculateData()
        {
            WholeSetCahnge(0, 0);
            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
        }
        public bool RecalculateData(CustomeGridRow row, GridColumn column,  bool localChange)
        {
            int changedColumn = (int)column;
            int currentIndex = Data.IndexOf(row);
            int prevIndex = currentIndex - 1;
            int nextIndex = currentIndex + 1;
            bool isClear = true;

            if (prevIndex >= 0 && prevIndex < Data.Count - 1)
            {
                if (decimal.IsNegative(Data[currentIndex].Col3) && Data.Count > nextIndex && !decimal.IsNegative(Data[nextIndex].Col2))
                {
                    changedColumn = 1;
                    decimal speed = (decimal)Math.Round((Data[nextIndex].Col2 - Data[currentIndex].Col2) / (Data[nextIndex].Col1 - Data[currentIndex].Col1), 3);
                    if (Data[currentIndex].Col3 != speed)
                        Data[currentIndex].Col3 = speed;
                }
                else if (decimal.IsNegative(Data[currentIndex].Col2) && !decimal.IsNegative(Data[prevIndex].Col3))
                {
                    changedColumn = 2;
                    decimal fedg = (decimal)Math.Round((Data[prevIndex].Col2 + (Data[currentIndex].Col1 - Data[prevIndex].Col1) * Data[prevIndex].Col3), 3);
                    if (Data[currentIndex].Col2 != fedg)
                        Data[currentIndex].Col2 = fedg;
                }

                if (localChange)
                    LocalChangeSet(currentIndex, changedColumn);
                else
                    WholeSetCahnge(currentIndex, changedColumn);

            }

            if (currentIndex < Data.Count - 1 && Data[currentIndex].Col2 > Data[nextIndex].Col2)
                isClear = false;

            if (currentIndex == 0 && Data.Count == 1 && decimal.IsNegative(Data[currentIndex].Col3))
                Data[currentIndex].Col3 = 0;
            if (currentIndex == 0 && Data.Count == 1 && decimal.IsNegative(Data[currentIndex].Col2))
                Data[currentIndex].Col2 = 0;

            _db.UpdateData(_tableName, Data.Cast<MainGridRow>().ToList());
            return isClear;
        }
        private void LocalChangeSet(int currentIndex, int changedColumn)
        {
            int prevIndex = currentIndex - 1;
            CustomeGridRow previousData = Data[prevIndex];
            CustomeGridRow currentData = Data[currentIndex];
            CustomeGridRow nextData = null;
            if (currentIndex + 1 < Data.Count)
                nextData = Data[currentIndex + 1];
            switch (changedColumn)
            {
                case 0:
                    currentData.Col2 = previousData.Col2 + (currentData.Col1 - previousData.Col1) * previousData.Col3;
                    if (nextData != null)
                        currentData.Col3 = (nextData.Col2 - currentData.Col2) / (nextData.Col1 - currentData.Col1);
                    break;
                case 1:
                    previousData.Col3 = (currentData.Col2 - previousData.Col2) / (currentData.Col1 - previousData.Col1);
                    if (nextData != null)
                        currentData.Col3 = (nextData.Col2 - currentData.Col2) / (nextData.Col1 - currentData.Col1);
                    break;
                case 2:
                    if (nextData != null)
                    {
                        currentData.Col2 = nextData.Col2 - (nextData.Col1 - currentData.Col1) * currentData.Col3;
                        previousData.Col3 = (currentData.Col2 - previousData.Col2) / (currentData.Col1 - previousData.Col1);
                    }
                    break;
            }
        }
        private void WholeSetCahnge(int currentIndex, int changedColumn)
        {
            if (changedColumn >= 1)
            {
                if (changedColumn == 1 && currentIndex - 1 >= 0)
                {
                    CustomeGridRow data = Data[currentIndex];
                    CustomeGridRow prevData = Data[currentIndex - 1];
                    prevData.Col3 = (data.Col2 - prevData.Col2) / (data.Col1 - prevData.Col1);
                }
                currentIndex++;
            }

            for (int i = currentIndex; i < Data.Count; i++)
            {
                if (i > 0)
                {
                    CustomeGridRow data = Data[i];
                    CustomeGridRow prevData = Data[i - 1];
                    data.Col2 = Math.Round( prevData.Col2 + (data.Col1 - prevData.Col1) * prevData.Col3, 3);
                }
            }
        }
    }

    public class CustomeGridRow : MainGridRow
    {
        public bool IsEditing { get; set; }
        public bool JustMoved { get; set; }
        public bool IsErrorInLine { get; set; }
        public bool IsNewLine { get; set; }
        public bool IsSelected { get; set; }
        public bool RowChanged { get; set; }
        public bool CommitInProgress { get; set; }
        public ElementReference ElementRef { get; set; } // for scroll
        public string RowId { get; set; } = Guid.NewGuid().ToString();
    }
    public class MainGridRow
    {
        public decimal Col1 { get; set; } 
        public decimal Col2 { get; set; }
        public decimal Col3 { get; set; }
    }
}
