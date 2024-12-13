namespace J.Server;

public static class ExternalShared
{
    public static string SharedCss { get; } =
        """
            body {
              background-color: #1a1b1e;
              color: #e1e2e4;
              font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen-Sans, Ubuntu, Cantarell, "Helvetica Neue", sans-serif;
              line-height: 1.6;
              max-width: 1200px;
              margin: 0 auto;
              padding: 2rem;
            }

            h1 {
              color: #ffffff;
              font-size: 2rem;
              font-weight: 600;
              margin-bottom: 1.5rem;
            }

            hr {
              border: none;
              border-top: 1px solid #333438;
              margin: 2rem 0;
            }

            ul {
              list-style: none;
              padding: 0;
              margin: 0;
            }

            li {
              margin: 0.5rem 0;
              background-color: #25262b;
              border-radius: 6px;
              transition: background-color 0.2s ease;
            }

            li:hover {
              background-color: #2c2d32;
            }

            a {
              color: #69b4ff;
              text-decoration: none;
              display: block;
              position: relative;
              padding: 0.75rem 1rem;
              padding-left: 2.5rem;
            }

            /* File and folder icons using classes */
            .back, .file, .folder {
              position: relative;
            }

            .back::before, .file::before, .folder::before {
              position: absolute;
              left: 1rem;
              top: 50%;
              transform: translateY(-50%);
            }

            .back::before {
              content: "↩️";
            }

            .folder::before {
              content: "📁";
            }

            .file::before {
              content: "📄";
            }

            /* Responsive design */
            @media (max-width: 768px) {
              body {
                padding: 1rem;
              }

              h1 {
                font-size: 1.5rem;
              }

              li {
                padding: 0.5rem 0.75rem;
              }
            }
            """;
}
