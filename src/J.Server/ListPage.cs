using System.Drawing;
using System.Net;
using System.Text.Json;
using J.Core;

namespace J.Server;

public static class ListPage
{
    private static string HtmlColor(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    public static Html GenerateHtml(
        List<PageBlock> pageBlocks,
        List<string> metadataKeys,
        string Title,
        string sessionPassword
    )
    {
        var blockJsons = pageBlocks.Select(x => new PageBlockJson(x, sessionPassword)).ToList();

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(Title)}}</title>
                <link href="/static/tabulator.min.css" rel="stylesheet">
                <script src="/static/tabulator.min.js"></script>
                <style>
                    {{PageShared.SharedCss}}

                    .tabulator {
                        background-color: transparent;
                        border: none;
                    }

                    .tabulator-header {
                        background-color: #3d3d3d;
                        border-top: 1px solid #999999;
                    }

                    .tabulator-col {
                        background: transparent;
                    }

                    .tabulator-col-title {
                        color: #fff !important;
                    }

                    .tabulator-header, .tabulator-col {
                        background-color: #3d3d3d !important;
                    }

                    .tabulator-col-resize-handle {
                        width: 8px !important;
                        margin-left: -4px;
                        margin-right: -4px;
                    }

                    .tabulator-col-resize-handle:hover {
                        background: #555;
                    }

                    .tabulator-col-content {
                        padding: 4px 16px !important;
                        line-height: 32px !important;
                    }

                    .tabulator-col:hover {
                        background: #3d3d3d;
                    }

                    .tabulator-row {
                        color: #fff;
                        margin: 0 !important;
                        padding: 0 !important;
                    }

                    .tabulator-row .tabulator-cell {
                        padding: 0px 16px !important;
                        line-height: 32px !important;
                        border-right: none;
                    }

                    .tabulator-row, .tabulator-cell {
                        border: none !important;
                    }

                    .tabulator-row.tag-row .tabulator-cell {
                        padding: 0px 16px !important;
                        line-height: 48px !important;
                        font-size: 20px;
                        font-weight: 500;
                    }

                    .tabulator-row:not(.tag-row) {
                        background-color: #1a1a1a;
                    }

                    .tabulator-row:hover {
                        background-color: #2a2a2a !important;
                    }

                    .tabulator-row.tag-row:hover {
                        background-color: #3a4556 !important;
                    }

                    /* Alternating row colors */
                    .tabulator-row:nth-child(even) {
                        background-color: #1a1a1a !important;
                    }

                    .tabulator-row:nth-child(odd) {
                        background-color: #2a2a2a !important;
                    }

                    .tabulator-row.tabulator-selected {
                        background-color: {{HtmlColor(MyColors.WebListSelection)}} !important;
                    }

                    .tabulator-cell {
                        cursor: default !important;
                    }
                </style>
            </head>
            <body>
                <div id="table"></div>

                <script>
                    {{PageShared.GetSharedJs(sessionPassword)}}

                    const items = {{JsonSerializer.Serialize(blockJsons)}};
                    const metadataKeys = {{JsonSerializer.Serialize(metadataKeys)}};

                    const columns = [
                        {
                            title: "Name", 
                            field: "title",
                            width: 600,
                            resizable: true,
                            formatter: (cell) => {
                                const value = cell.getValue();
                                const row = cell.getRow();
                                const data = row.getData();
                                return data.id.startsWith('tag-') ? `📁 ${value}` : value;
                            }
                        },
                        ...metadataKeys.map(key => ({
                            title: key,
                            field: `metadata.${key}`,
                            width: 200,
                            resizable: true,
                            formatter: (cell) => cell.getValue()?.toString() || ''
                        }))
                    ];

                    const table = new Tabulator("#table", {
                        data: items,
                        columns: columns,
                        layout: "fixed", // Uses fixed widths but allows manual resizing
                        virtualDom: true,
                        height: "100vh",
                        rowFormatter: function(row) {
                            const data = row.getData();
                            const element = row.getElement();

                            if (data.id.startsWith('tag-')) {
                                element.classList.add('tag-row');
                            }
                        },
                        selectableRows: true, // Enable selection
                        selectableRowsRangeMode: "click", // Shift+click for ranges
                        selectMode: "multiple" // Allow multiple rows to be selected
                    });

                    table.on("rowContext", function(e, row) {
                        if (!row.isSelected()) {
                            table.deselectRow();
                            row.select();
                        }
                        const selectedIds = table.getSelectedRows().map(row => row.getData().id);
                        menu(selectedIds);
                    });

                    table.on("rowDblClick", function(e, row) {
                        const data = row.getData();
                        open(data.id);
                    });
                </script>
            </body>
            </html>
            """;

        return new(html);
    }
}
