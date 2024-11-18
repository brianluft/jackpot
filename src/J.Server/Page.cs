using System.Net;
using System.Text.Json;
using System.Web;
using J.Core.Data;

namespace J.Server;

public readonly record struct Page(List<Page.Block> Blocks, string Title)
{
    public readonly record struct Block(
        MovieId MovieId, // movie to get the clip from
        TagId? TagId, // if targeting a tag instead of a movie on click, this is the tag
        string Title,
        DateTimeOffset Date,
        Dictionary<TagTypeId, string> SortTags
    );

#pragma warning disable IDE1006 // Naming Styles
    private readonly record struct BlockJson(string id, string url, string title);
#pragma warning restore IDE1006 // Naming Styles

    public string ToHtml(string sessionPassword, int columnCount)
    {
        List<BlockJson> blockJsons = new(Blocks.Count);

        foreach (var block in Blocks)
        {
            var query = HttpUtility.ParseQueryString("");
            query["sessionPassword"] = sessionPassword;
            query["movieId"] = block.MovieId.Value;

            blockJsons.Add(new(block.TagId?.Value ?? block.MovieId.Value, $"/clip.mp4?{query}", block.Title));
        }

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <title>{{WebUtility.HtmlEncode(Title)}}</title>
                <style>
                    * {
                        user-select: none;
                    }

                    body {
                        margin: 0;
                        padding: 0;
                        overflow: hidden;
                    }

                    .grid-container {
                        position: relative;
                        height: 100vh;
                        overflow-y: scroll;
                        background: black;
                        padding: 4px;
                        box-sizing: border-box;
                    }

                    .video-cell {
                        position: absolute;
                        cursor: pointer;
                        box-sizing: border-box;
                    }

                    video {
                        width: 100%;
                        height: 100%;
                        object-fit: cover;
                        pointer-events: none;
                    }

                    .title-bar {
                        position: absolute;
                        bottom: -1px;  /* Ensure no gap between video bottom and title bottom */
                        left: 0;
                        right: 0;
                        height: 22px; /* Includes extra 1px for the extra bottom: -1px */
                        background: rgba(0, 0, 0, 0.5);
                        padding: 0 6px;
                        display: flex;
                        align-items: center;
                        pointer-events: none;
                        justify-content: center; 
                    }

                    .title-bar-tag {
                        height: 43px;  /* Double height, plus 1px for the extra bottom: -1px */
                        background: rgba(0, 0, 0, 0.5);
                        justify-content: left;
                    }

                    .title-text {
                        color: white;
                        font-size: 12px;
                        white-space: nowrap;
                        overflow: hidden;
                        text-overflow: ellipsis;
                        font-family: system-ui, -apple-system, sans-serif;
                        font-weight: bold;
                        transform: translateY(-1px);  /* Account for the extra bottom: -1px */
                    }

                    .title-text-tag {
                        font-size: 24px;  /* Larger font */
                    }

                    .virtual-height {
                        width: 100%;
                        pointer-events: none;
                    }
                </style>
            </head>
            <body>
                <div class="grid-container" id="gridContainer">
                    <div class="virtual-height" id="virtualHeight"></div>
                </div>

                <script>
                    // Disable context menu page-wide
                    document.addEventListener('contextmenu', e => e.preventDefault());

                    // Capture Ctrl+F and / to focus the search box
                    document.addEventListener('keydown', function(e) {
                        if ((e.ctrlKey && e.key === 'f') || (!e.ctrlKey && e.key === '/')) {
                            e.preventDefault();
                            window.chrome.webview.postMessage(JSON.stringify({ Type: 'search' }));
                        }
                    });

                    // Video data injected from C#
                    const VIDEOS = {{JsonSerializer.Serialize(blockJsons)}};

                    // Horizontal grid size only
                    const GRID_SIZE = {{columnCount}};

                    // Buffer rows above and below visible area
                    const BUFFER = 2;

                    // Gap between cells
                    const GAP = 4;

                    const gridContainer = document.getElementById('gridContainer');
                    const virtualHeight = document.getElementById('virtualHeight');
                    const activeVideos = new Map();

                    function openMovie(movieId) {
                        // Escape the movieId to safely use it in the URL
                        const escapedMovieId = encodeURIComponent(movieId);

                        // Create the full URL with the escaped movieId
                        const url = `/open-movie?sessionPassword={{sessionPassword}}&movieId=${escapedMovieId}`;

                        // Send a POST request
                        fetch(url, { method: 'POST' })
                            .then(() => {
                                // cool
                            })
                            .catch(error => {
                                alert('Could not open movie: ' + error.message);
                            });
                    }

                    function openTag(id) {
                        location.href = '/tag.html?sessionPassword={{sessionPassword}}&tagId=' + encodeURIComponent(id) + '&pageIndex=0';
                    }

                    function open(id) {
                        if (id.startsWith("movie-")) {
                            openMovie(id);
                        } else if (id.startsWith("tag-")) {
                            openTag(id);
                        } else {
                            alert('Invalid ID: ' + id);
                        }
                    }

                    function calculateDimensions() {
                        const containerWidth = gridContainer.clientWidth - (2 * GAP); // subtract container padding
                        const cellWidth = (containerWidth - (GAP * (GRID_SIZE - 1))) / GRID_SIZE;
                        const cellHeight = (cellWidth * 9) / 16;
                        const totalRows = Math.ceil(VIDEOS.length / GRID_SIZE);
                        const rowHeight = cellHeight + GAP;
                        return { cellWidth, cellHeight, rowHeight, totalRows };
                    }

                    function setVirtualHeight() {
                        const { rowHeight, totalRows } = calculateDimensions();
                        const totalHeight = rowHeight * totalRows;
                        virtualHeight.style.height = `${totalHeight}px`;
                    }

                    function calculatePosition(index) {
                        const { cellWidth, cellHeight } = calculateDimensions();
                        const row = Math.floor(index / GRID_SIZE);
                        const col = index % GRID_SIZE;

                        const left = GAP + (col * (cellWidth + GAP));
                        const top = GAP + (row * (cellHeight + GAP));

                        return { left, top, width: cellWidth, height: cellHeight };
                    }

                    function createVideoElement(index) {
                        const videoData = VIDEOS[index];
                        if (!videoData) return null;

                        const cell = document.createElement('div');
                        cell.className = 'video-cell';

                        const pos = calculatePosition(index);
                        cell.style.left = `${pos.left}px`;
                        cell.style.top = `${pos.top}px`;
                        cell.style.width = `${pos.width}px`;
                        cell.style.height = `${pos.height}px`;

                        cell.addEventListener('click', () => {
                            if (typeof open === 'function') {
                                open(videoData.id);
                            }
                        });

                        const video = document.createElement('video');
                        video.loop = true;
                        video.muted = true;
                        video.style.display = 'none';

                        const source = document.createElement('source');
                        source.src = videoData.url;
                        source.type = 'video/mp4';
                        video.appendChild(source);

                        const titleBar = document.createElement('div');
                        const titleText = document.createElement('div');

                        if (videoData.id.startsWith('tag-')) {
                            titleBar.className = 'title-bar title-bar-tag';
                            titleText.className = 'title-text title-text-tag';
                            titleText.textContent = '📁 ' + videoData.title;
                        } else {
                            titleBar.className = 'title-bar';
                            titleText.className = 'title-text';
                            titleText.textContent = videoData.title;
                        }

                        titleBar.appendChild(titleText);

                        cell.appendChild(video);
                        cell.appendChild(titleBar);

                        return cell;
                    }

                    function calculateVisibleRange() {
                        const { rowHeight } = calculateDimensions();
                        const scrollTop = gridContainer.scrollTop;
                        const viewportHeight = gridContainer.clientHeight;

                        const startRow = Math.max(0, Math.floor(scrollTop / rowHeight) - BUFFER);
                        const visibleRows = Math.ceil(viewportHeight / rowHeight) + (BUFFER * 2);
                        const endRow = Math.min(Math.ceil(VIDEOS.length / GRID_SIZE), startRow + visibleRows);

                        return {
                            start: startRow * GRID_SIZE,
                            end: Math.min(endRow * GRID_SIZE, VIDEOS.length)
                        };
                    }

                    function updateVisibleVideos() {
                        const visibleRange = calculateVisibleRange();

                        // Remove out-of-view videos
                        for (const [key, element] of activeVideos) {
                            const index = parseInt(key);
                            if (index < visibleRange.start || index >= visibleRange.end) {
                                const video = element.querySelector('video');
                                if (video) {
                                    video.pause();
                                    video.style.display = 'none';
                                }
                                element.remove();
                                activeVideos.delete(key);
                            }
                        }

                        // Add new visible videos
                        for (let i = visibleRange.start; i < visibleRange.end; i++) {
                            if (!activeVideos.has(i.toString())) {
                                const cell = createVideoElement(i);
                                if (cell) {
                                    gridContainer.appendChild(cell);
                                    activeVideos.set(i.toString(), cell);
                                    const video = cell.querySelector('video');
                                    if (video) {
                                        video.style.display = 'block';
                                        video.play().catch(() => {});
                                    }
                                }
                            }
                        }
                    }

                    // Scroll handler with debounce
                    let scrollTimeout;
                    gridContainer.addEventListener('scroll', () => {
                        if (scrollTimeout) return;  // Skip if already waiting
                        scrollTimeout = setTimeout(() => {
                            updateVisibleVideos();
                            scrollTimeout = null;
                        }, 150);
                    });

                    // Window resize handler with debounce
                    let resizeTimeout;
                    window.addEventListener('resize', () => {
                        if (resizeTimeout) clearTimeout(resizeTimeout);
                        resizeTimeout = setTimeout(() => {
                            setVirtualHeight();
                            // Update positions of existing videos
                            for (const [index, element] of activeVideos) {
                                const pos = calculatePosition(parseInt(index));
                                element.style.left = `${pos.left}px`;
                                element.style.top = `${pos.top}px`;
                                element.style.width = `${pos.width}px`;
                                element.style.height = `${pos.height}px`;
                            }
                            updateVisibleVideos();
                        }, 150);
                    });

                    // Initial setup
                    setVirtualHeight();
                    updateVisibleVideos();
                </script>
            </body>
            </html>
            """;

        return html;
    }
}
