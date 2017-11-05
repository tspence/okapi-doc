using OkapiDoc.Render;
using System.Collections.Generic;

namespace OkapiDoc
{
    public class SwaggerRenderTask
    {
        /// <summary>
        /// The URL of the overall swagger file we are going to download
        /// </summary>
        public string swaggerUri { get; set; }

        /// <summary>
        /// The list of rendering targets we will use
        /// </summary>
        public List<RenderTarget> targets { get; set; }
    }
}
