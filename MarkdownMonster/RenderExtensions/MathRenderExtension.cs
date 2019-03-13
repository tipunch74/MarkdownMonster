﻿namespace MarkdownMonster.RenderExtensions
{
    /// <summary>
    /// Handle MathText and MathML in the document using $$ for block operations and $ for inline
    /// Math expressions
    /// </summary>
    public class MathRenderExtension : IRenderExtension
    {

        /// <summary>
        /// Embeds the MathJax header and processes dynamically when the document is updated.
        /// </summary>
        /// <param name="html"></param>
        /// <param name="markdown"></param>
        /// <param name="document"></param>
        /// <returns></returns>
        public string RenderHeader(string html, string markdown, MarkdownDocument document)
        {
            if (markdown.Contains(" class=\"math\""))                
                return MathJaxScript;

            return null;
        }


        /// <summary>
        /// No content is added by this extension - it's all handled via script header and javascript events
        /// </summary>
        /// <param name="html"></param>
        /// <param name="markdown"></param>
        /// <param name="document"></param>
        public void InsertContent(ref string html, string markdown, MarkdownDocument document)
        {

        }

        public void BeforeRender(ref string markdown, MarkdownDocument document)
        {
        }


        public bool ShouldProcessBeforeRender(string markdown, MarkdownDocument document) => false;

        public const string MathJaxScript = @"
<script type=""text/x-mathjax-config"">
    // enable inline parsing with single $ instead of /
    MathJax.Hub.Config({
        tex2jax: {
            //inlineMath: [['$','$'],['\\(','\\)']],
            //displayMath: [ ['$$','$$'], ['\\[','\\]'] ],
            processEscapes: true
        },
        //asciimath2jax: {
        //    delimiters: [['`','`']]
        //},
        TeX: {
            extensions: ['autoload-all.js']
        }
    });

    // refresh when the document is refreshed via code
    $(document).on('previewUpdated',function() {
        setTimeout(function() {
                    MathJax.Hub.Queue(['Typeset',MathJax.Hub,'#MainContent']);
        },10);
    });
</script>
<style>
    span.math span.MJXc-display {
        display: inline-block;
    }
</style>
<script src=""https://cdnjs.cloudflare.com/ajax/libs/mathjax/2.7.5/latest.js?config=TeX-MML-AM_CHTML"" async></script>
";




    }
}
