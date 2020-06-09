﻿using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
#if NET45
using System.Web.Mvc;
#else
using Microsoft.AspNetCore.Mvc;
#endif

namespace Griddly.Mvc
{
    public class GriddlyExcelResult<T> : ActionResult
    {
        public static bool EnableAutoFilter { get; set; } = true;

        IEnumerable<T> _data;
        GriddlySettings _settings;
        string _name;
        string _exportName;

        public GriddlyExcelResult(IEnumerable<T> data, GriddlySettings settings, string name, string exportName = null)
        {
            _data = data;
            _settings = settings;
            _name = name;
            _exportName = exportName;
        }

        static readonly Regex _htmlMatch = new Regex(@"<[^>]*>", RegexOptions.Compiled);
        // static readonly Regex _aMatch = new Regex(@"<a\s[^>]*\s?href=""(.*?)"">(.*?)</a>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

#if NET45
        public override void ExecuteResult(ControllerContext context)
#else
        public override void ExecuteResult(ActionContext context)
#endif
        {
            using (ExcelPackage p = new ExcelPackage())
            {
                ExcelWorksheet ws = p.Workbook.Worksheets.Add(_name);

                var export = _settings.Exports.FirstOrDefault(x => x.Name == _exportName);
                var columns = export == null ? _settings.Columns : export.Columns;

                if (export != null && export.UseGridColumns)
                    columns.InsertRange(0, _settings.Columns);

                columns.RemoveAll(x => !x.Visible || !x.RenderMode.HasFlag(ColumnRenderMode.Export));

                for (int i = 0; i < columns.Count; i++)
                {
                    string caption = HttpUtility.HtmlDecode(_htmlMatch.Replace(columns[i].Caption, "").Trim().Replace("  ", " "));
                    ws.Cells[1, i + 1].Value = caption;
                    ws.Cells[1, i + 1].Style.Font.Bold = true;
                }

                int y = 0;

                foreach (T row in _data)
                {
                    for (int x = 0; x < columns.Count; x++)
                    {
                        object renderedValue = columns[x].RenderCellValue(row, true);

                        ExcelRange cell = ws.Cells[y + 2, x + 1];

                        cell.Value = renderedValue;

                        if (renderedValue as DateTime? != null)
                        {
                            if (columns[x].Format == "f" || columns[x].Format == "F"
                                || columns[x].Format == "g" || columns[x].Format == "G")
                                cell.Style.Numberformat.Format = "mm/dd/yyyy hh:mm";
                            else
                                cell.Style.Numberformat.Format = "mm/dd/yyyy";

                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }
                        else if (columns[x].Format == "c")
                        {
                            if (GriddlySettings.ExportCurrencySymbol)
                                cell.Style.Numberformat.Format = "\"" + GriddlyExtensions.CurrencySymbol + "\"#,##0.00_);(\"" + GriddlyExtensions.CurrencySymbol + "\"#,##0.00)";

                            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                        }
                        else if (renderedValue is string stringValue && stringValue.IndexOf('\n') != -1)
                        {
                            cell.Style.WrapText = true;
                        }
                    }

                    y++;
                }

                for (int i = 0; i < columns.Count; i++)
                {
                    GriddlyColumn col = columns[i];
                    if (col.ExportWidth != null)
                        ws.Column(i + 1).Width = col.ExportWidth.Value;
                    else
                        ws.Column(i + 1).AutoFit(8, 80);
                }

                if (EnableAutoFilter)
                    ws.Cells[1, 1, 1 + y, columns.Count].AutoFilter = true;

                context.HttpContext.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                context.HttpContext.Response.Headers.Add("content-disposition", "attachment;  filename=" + _name + ".xlsx");

#if NET45
                p.SaveAs(context.HttpContext.Response.OutputStream);
#else
                p.SaveAs(context.HttpContext.Response.Body);
#endif
            }
        }
    }
}
