﻿using AngleSharp.Dom;
using ColorCode.Compilation.Languages;

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Windows.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.Text;
using Microsoft.UI;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;

namespace RichTextControls.Generators
{
    public class HtmlXamlGenerator : IHtmlXamlGenerator
    {
        private readonly HtmlParser _parser;
        private readonly string _html;
        private IHtmlDocument _document;

        private static readonly Regex _htmlWhitespaceRegex = new Regex(@"(?<=\s)\s+(?![^<pre>]*</pre>)", RegexOptions.Compiled);
        private static readonly Regex _preTagRegex = new Regex(@"(?:\<pre\>)(.*)(?:\<\/pre\>)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        protected ILinkRegister LinkRegister { get; }

        public HtmlXamlGenerator(string html, ILinkRegister linkRegister)
        {
            _parser = new HtmlParser();
            _html = PrepareRawHtml(html); 
            LinkRegister = linkRegister;

        }

        public HtmlXamlGenerator(IHtmlDocument document)
        {
            _document = document;
        }

        /// <summary>
        /// The <see cref="Style"/> to be applied to <see cref="Border"/> encapsulating everything between <blockquote></blockquote> HTML tags.
        /// </summary>
        public Style BlockquoteBorderStyle
        {
            get;
            set;
        }

        /// <summary>
        /// The <see cref="Style"/> to be applied to <see cref="Border"/> encapsulating everything between <pre></pre> HTML tags.
        /// </summary>
        public Style PreformattedBorderStyle
        {
            get;
            set;
        }

        /// <summary>
        /// Parses the HTML and generates elements from the children.
        /// </summary>
        /// <returns>A <see cref="UIElement"/> representing the HTML.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no parser is detected. Typically occurs when subclassed without calling base constructor.</exception>
        public UIElement Generate()
        {
            if (_parser == null && _document == null)
                throw new InvalidOperationException("No HTML parser was set. If this is a subclass you must instantiate the parent with `base()`.");

            _document = _document ?? _parser.ParseDocument(_html);

            var panel = new StackPanel ();

            AddChildren(_document.Body, panel.Children);

            return panel;
        }

        /// <summary>
        /// Creates <see cref="Inline"/> elements which are ones that can be nested inside of a <p> tag:
        /// <acronym>, <b>, <big>, <br/>, <button>, <cite>, <code>, <dfn>, <em>, <font>, <i>, <iframe>, <img>, <input>, <kbd>, <label>, <q>, <s>, <samp>, <script>, <select>, <small>, <span>, <strike>, <strong>, <sub>, <sup>, <textarea>, <tt>, <u>, <var>
        /// </summary>
        /// <param name="node">The <see cref="INode"/> to generate the <see cref="Inline"/> from.</param>
        /// <param name="inlines">The <see cref="InlineCollection"/> of the parent element.</param>
        /// <returns>The <see cref="Inline"/> to be appended to the parent.</returns>
        protected virtual Inline GenerateInlineForNode(INode node, InlineCollection inlines)
        {
            switch (node.NodeName)
            {
                case "S":
                case "STRIKE":
                    return GenerateStrike(node);
                case "IMG":
                    return GenerateInlineImage(node as IHtmlImageElement);
                case "A":
                    return GenerateLink(node as IHtmlAnchorElement);
                case "STRONG":
                case "B":
                    return GenerateBold(node);
                case "I":
                case "EM":
                case "CITE":
                    return GenerateItalic(node);
                case "INS":
                case "U":
                    return GenerateUnderline(node);
                case "BR":
                    return GenerateLineBreak();
                case "HR":
                    return GenerateHorizontalRule();
                case "SPAN":
                    return GenerateSpan(node);
                case "CODE":
                    return GenerateCode(node);
                case "Q":
                    return GenerateQuote(node as IHtmlQuoteElement);
                case "ABBR":
                    return GenerateAbbreviation(node as IHtmlElement);
                case "MARK":
                    return GenerateMark(node);
                case "SMALL":
                    return GenerateSmall(node);
                case "LI":
                    return GenerateLi(node);
                // Technically `IFRAME` is allowed as an inline, but this is not common
                // so we have no default handling for it.
                case "#text":
                default:
                    return GeneratePlainText(node);
            }
        }

        private Inline GenerateLi(INode node)
        {
            var span = new Span();
            span.Inlines.Add(new LineBreak());
            span.Inlines.Add(new Run { Text = "  •  " });
            AddChildren(node, span);
            return span;
        }

        /// <summary>
        /// Creates <see cref="UIElement"/> elements which can be added to a <see cref="UIElementCollection"/>.
        /// </summary>
        /// <param name="node">The <see cref="INode"/> to generate the <see cref="UIElement"/> from.</param>
        /// <param name="elements">The <see cref="UIElementCollection"/> of the parent element.</param>
        /// <returns>The <see cref="UIElement"/> to be appended to the parent.</returns>
        protected virtual UIElement GenerateUIElementForNode(INode node, UIElementCollection elements)
        {
            RichTextBlock lastTextBlock = null;
            switch (node.NodeName)
            {
                case "S":
                case "STRIKE":
                    var strike = GenerateStrike(node);
                    return AddInlineToTextBlock(elements, strike);
                case "DIV":
                    var div = (IHtmlDivElement)node;
                    if (div.Attributes["class"] != null)
                    {
                        if (div.Attributes["class"].Value.Contains("alert"))
                            return GenerateInfoBar(div);
                    }
                    return GenerateDiv(node);
                case "LI": // Treat <li> outside of a <ul> or <ol> as regular Paragraph.
                case "P":
                    var paragraph = GenerateParagraph(node as IHtmlParagraphElement);
                    lastTextBlock = GetOrCreateLastRichTextBlock(elements);
                    lastTextBlock.Blocks.Add(paragraph);
                    return lastTextBlock;
                case "IMG":
                    return GenerateImage(node as IHtmlImageElement);
                case "A":
                    var link = GenerateLink(node as IHtmlAnchorElement);
                    return AddInlineToTextBlock(elements, link);
                case "BLOCKQUOTE":
                    return GenerateBlockQuote(node);
                case "STRONG":
                case "B":
                    var bold = GenerateBold(node);
                    return AddInlineToTextBlock(elements, bold);
                case "I":
                case "EM":
                case "CITE":
                    var italic = GenerateItalic(node);
                    return AddInlineToTextBlock(elements, italic);
                case "INS":
                case "U":
                    var underline = GenerateUnderline(node);
                    return AddInlineToTextBlock(elements, underline);
                case "HR":
                    var rule = GenerateHorizontalRule();
                    return AddInlineToTextBlock(elements, rule, new Paragraph());
                case "BR":
                    var linebreak = GenerateLineBreak();
                    return AddInlineToTextBlock(elements, linebreak);
                case "SPAN":
                    var span = GenerateSpan(node);
                    return AddInlineToTextBlock(elements, span);
                case "IFRAME":
                    return GenerateIframe(node as IHtmlInlineFrameElement);
                case "H1":
                case "H2":
                case "H3":
                case "H4":
                case "H5":
                case "H6":
                    lastTextBlock = GetOrCreateLastRichTextBlock(elements);
                    var headerParagraph = new Paragraph()
                    {
                        Margin = new Thickness(0, 19.5, 0, 3)
                    };
                    var header = GenerateHeader(node);
                    headerParagraph.Inlines.Add(header);
                    lastTextBlock.Blocks.Add(headerParagraph);
                    return lastTextBlock;
                case "OL":
                    return GenerateOL(node as IHtmlOrderedListElement);
                case "UL"://无序列表
                    var ul = GenerateUL(node as IHtmlUnorderedListElement);
                    return AddInlineToTextBlock(elements, ul);
                case "CODE":
                    var code = GenerateCode(node);
                    return AddInlineToTextBlock(elements, code);
                case "PRE":
                    return GeneratePreformatted(node as IHtmlPreElement);
                case "Q":
                    var quote = GenerateQuote(node as IHtmlQuoteElement);
                    return AddInlineToTextBlock(elements, quote);
                case "ABBR":
                    var abbreviation = GenerateAbbreviation(node as IHtmlElement);
                    return AddInlineToTextBlock(elements, abbreviation);
                case "MARK":
                    var mark = GenerateMark(node);
                    return AddInlineToTextBlock(elements, mark);
                case "SMALL":
                    var small = GenerateSmall(node);
                    return AddInlineToTextBlock(elements, small);
                case "#text":
                default:
                    var plainText = GeneratePlainText(node);
                    return AddInlineToTextBlock(elements, plainText);
            }
        }

        /// <summary>
        /// Loops through children of an <see cref="INode"/> and appends as <see cref="UIElement"/> to given <see cref="UIElementCollection"/>.
        /// </summary>
        /// <param name="node">The parent <see cref="INode"/>.</param>
        /// <param name="elements">The <see cref="UIElementCollection"/> collection to add elements to.</param>
        protected void AddChildren(INode node, UIElementCollection elements)
        {
            foreach (var child in node.ChildNodes)
            {
                var element = GenerateUIElementForNode(child, elements);

                if (elements.LastOrDefault() != element)
                    elements.Add(element);
            }
        }

        private void AddChildren(INode node,Span s)
        {
            var added = false;

            foreach (var child in node.ChildNodes)
            {
                var i = GenerateInlineForNode(child,s.Inlines);
                if (i != null)
                {
                    s.Inlines.Add(i);
                    added = true;
                }
            }
            if (!added)
            {
                s.Inlines.Add(new Run { Text = node.TextContent });
            }
        }

        /// <summary>
        /// Loops through children of an <see cref="INode"/> and appends as <see cref="Inline"/> to given <see cref="InlineCollection"/>.
        /// </summary>
        /// <param name="node">The parent <see cref="INode"/>.</param>
        /// <param name="inlines">The <see cref="InlineCollection"/> collection to add inlines to.</param>
        protected void AddInlineChildren(INode node, InlineCollection inlines)
        {
            foreach (var child in node.ChildNodes)
            {
                var inline = GenerateInlineForNode(child, inlines);

                if (inlines.LastOrDefault() != inline)
                    inlines.Add(inline);
            }
        }

        /// <summary>
        /// Given a <see cref="UIElementCollection"/>, appends the given <see cref="Inline"/> to a <see cref="Paragraph"/>.
        /// </summary>
        /// <param name="elements">The <see cref="UIElementCollection"/> collection to add elements to.</param>
        /// <param name="inline">The <see cref="Inline"/> to add to the collection.</param>
        /// <param name="paragraph">The <see cref="Paragraph"/> to add. Will get from collection or create one if none provided using <see cref="GetOrCreateLastParagraph(BlockCollection)"/>.</param>
        /// <returns></returns>
        protected UIElement AddInlineToTextBlock(UIElementCollection elements, Inline inline, Paragraph paragraph = null)
        {
            var lastTextBlock = GetOrCreateLastRichTextBlock(elements);
            paragraph = paragraph ?? GetOrCreateLastParagraph(lastTextBlock.Blocks);
            paragraph.Inlines.Add(inline);

            if (lastTextBlock.Blocks.LastOrDefault() != paragraph)
                lastTextBlock.Blocks.Add(paragraph);

            return lastTextBlock;
        }

        /// <summary>
        /// Gets a <see cref="RichTextBlock"/> from given <see cref="UIElementCollection"/> or creates and adds one.
        /// </summary>
        /// <param name="elements">The <see cref="UIElementCollection"/> collection to add <see cref="RichTextBlock"/> to.</param>
        /// <returns>The last <see cref="RichTextBlock"/> of the collection.</returns>
        protected RichTextBlock GetOrCreateLastRichTextBlock(UIElementCollection elements)
        {
            if (elements.LastOrDefault() is RichTextBlock textBlock)
                return textBlock;

            textBlock = new RichTextBlock { LineHeight = 24,FontSize = 15};
            elements.Add(textBlock);

            return textBlock;
        }

        private string PrepareRawHtml(string rawHtml)
        {
            if (String.IsNullOrWhiteSpace(rawHtml))
                return "";

            var preTags = new Dictionary<string, string>();
            string processedHtml = rawHtml;

            // Saves our <pre> tags in dictionary so the whitespace removal doesn't
            // strip from these tags. They'll get added back after that step.
            var matches = _preTagRegex.Matches(processedHtml);
            int currentIndex = 0;
            foreach (Match match in matches)
            {
                var key = $"{Guid.NewGuid().ToString()}_{currentIndex}";
                processedHtml = processedHtml.Replace(match.Value, key);
                preTags.Add(key, match.Value);
                currentIndex++;
            }

            // Removes whitespace between tags, which HTML does not normally render.
            processedHtml = _htmlWhitespaceRegex.Replace(processedHtml, String.Empty);
            processedHtml = processedHtml.Trim();
            // For some reason the regex leaves the returns (\r). 
            // TODO: Figure out why and remove this extra step.
            processedHtml = processedHtml.Replace("\r", String.Empty);

            // Adds our saved <pre> tags back to the HTML with whitespace preserved.
            foreach (var preMatch in preTags)
            {
                processedHtml = processedHtml.Replace(preMatch.Key, preMatch.Value);
            }

            return processedHtml;
        }

        private Paragraph GetOrCreateLastParagraph(BlockCollection blocks)
        {
            var lastBlock = blocks.LastOrDefault();
            if (lastBlock != null)
                return (Paragraph)lastBlock;

            var paragraph = new Paragraph();
            blocks.Add(paragraph);

            return paragraph;
        }

        private StackPanel GenerateDiv(INode node)
        {
            var stackPanel = new StackPanel();

            AddChildren(node, stackPanel.Children);

            return stackPanel;
        }

        private Paragraph GenerateParagraph(IHtmlParagraphElement node)
        {
            var paragraph = new Paragraph();

            AddInlineChildren(node, paragraph.Inlines);

            return paragraph;
        }

        private Inline GenerateUL(IHtmlUnorderedListElement node)
        {
            var span = new Span();
            AddChildren(node, span);
            return span;
        }

        /*private StackPanel GenerateUL(IHtmlUnorderedListElement node)
        {
            var verticalStackPanel = new StackPanel();

            foreach (var child in node.ChildNodes)
            {
                if (String.IsNullOrWhiteSpace(child.TextContent))
                    continue;

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                //var horizontalStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                //horizontalStackPanel.Children.Add(new TextBlock() { Text = "•", Margin = new Thickness(9.5, 0, 9.5, 0) });
                grid.Children.Add(new TextBlock() { Text = "•", Margin = new Thickness(9.5, 0, 9.5, 0) });

                var richTextBlock = new RichTextBlock();
                var paragraph = new Paragraph();

                AddInlineChildren(child, paragraph.Inlines);

                richTextBlock.Blocks.Add(paragraph);
                grid.Children.Add(richTextBlock);
                Grid.SetColumn(richTextBlock, 1);
                verticalStackPanel.Children.Add(grid);
            }

            return verticalStackPanel;
        }*/
        private InfoBar GenerateInfoBar(IHtmlDivElement node)
        {
            var span = new Span();
            //span.Inlines.Add(new LineBreak());
            var inlineUiContainer = new InlineUIContainer();
            var infobar = new InfoBar
            {
                IsClosable = false,
                Title = "信息",
                IsOpen = true,
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            };
            if (node.Attributes["class"].Value.Contains("error") || node.Attributes["class"].Value.Contains("warning"))
            {
                infobar.Title = "警告";
                infobar.Severity = InfoBarSeverity.Warning;
            }
            if (node.Attributes["class"].Value.Contains("success"))
            {
                infobar.Severity = InfoBarSeverity.Success;
            }

            var panel = new StackPanel { Margin = new Thickness(0,0,40,24)};

            AddChildren(node, panel.Children);

            infobar.Content = panel;
            return infobar;
        }

        private StackPanel GenerateOL(IHtmlOrderedListElement node)
        {
            var verticalStackPanel = new StackPanel ();

            int number = node.Start;
            foreach (var child in node.ChildNodes)
            {
                if (String.IsNullOrWhiteSpace(child.TextContent))
                    continue;

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
                //var horizontalStackPanel = new StackPanel() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                //horizontalStackPanel.Children.Add(new TextBlock() { Text = $"{number}.", Margin = new Thickness(9.5, 0, 9.5, 0) });
                grid.Children.Add(new TextBlock() { Text = $"{number}.", Margin = new Thickness(9.5, 0, 9.5, 0),TextWrapping=TextWrapping.Wrap});
                number++;

                var richTextBlock = new RichTextBlock();
                var paragraph = new Paragraph();

                AddInlineChildren(child, paragraph.Inlines);

                richTextBlock.Blocks.Add(paragraph);
                grid.Children.Add(richTextBlock);
                Grid.SetColumn(richTextBlock, 1);
                verticalStackPanel.Children.Add(grid);
            }

            return verticalStackPanel;
        }

        private ImageEx.ImageEx GenerateImage(IHtmlImageElement node)
        {

            var image = new ImageEx.ImageEx()
            {
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0,12,0,0),
                CornerRadius = new CornerRadius(8),
                PlaceholderStretch = Stretch.Uniform,
                PlaceholderSource = new BitmapImage(new Uri("ms-appx:///Assets/App/ImagePlaceHolder.png") ),
            };

            var sourceWidth = node.GetAttribute("width");
            var sourceHeight = node.GetAttribute("height");
            if (sourceWidth != null)
                image.MaxWidth = double.Parse(sourceWidth.Replace("px",""));
            if (sourceHeight != null)
                image.MaxHeight = double.Parse(sourceHeight.Replace("px", ""));

            var imageSrc = node.Source;
            image.Source = imageSrc;

            if (imageSrc.StartsWith("about"))//本地
            {
                image.Source = imageSrc.Remove(0, 5).Insert(0, "ms-appx");//替换为App内资源文件

                //image.MaxHeight = new BitmapImage(new Uri(imageSrc)).PixelHeight;
            }
            LinkRegister.RegisterNewHyperLink(image, imageSrc,false);

            /*if (Uri.TryCreate(node.Source, UriKind.RelativeOrAbsolute, out Uri src))
            {
                var bitmap = new BitmapImage(src);
                image.Source = bitmap;
            }
            else
                image.Source = node.Source;*/

            return image;
        }

        private Inline GenerateInlineImage(IHtmlImageElement node)
        {
            var inlineContainer = new InlineUIContainer();

            var image = GenerateImage(node);

            inlineContainer.Child = image;

            return inlineContainer;
        }

        private Inline GenerateLink(IHtmlAnchorElement node)
        {

            if (node.TextContent.Contains("http"))
            {
                var hyperlink = new Hyperlink();

                if (Uri.TryCreate(node.Href, UriKind.RelativeOrAbsolute, out Uri hrefUri))
                {
                    //hyperlink.NavigateUri = hrefUri;
                    LinkRegister.RegisterNewHyperLink(hyperlink, hrefUri.ToString());

                }
                // TODO: Add option for unfurling links as images
                // TODO: Add link clicked event

                AddInlineChildren(node, hyperlink.Inlines);

                return hyperlink;

            }
            else
                return GenerateLinkBtn(node);
            // TODO: Add option for unfurling links as images
            // TODO: Add link clicked event

        }
        public Inline GenerateLinkBtn(IHtmlAnchorElement node)
        {
            var hyperlink = new HyperlinkButton();

            if (Uri.TryCreate(node.Href, UriKind.RelativeOrAbsolute, out Uri hrefUri))
            {
                //hyperlink.NavigateUri = hrefUri;
                hyperlink.Margin = new Thickness(0, 0, 0, -10);
                LinkRegister.RegisterNewHyperLink(hyperlink, hrefUri.ToString());

            }
            var stack = new StackPanel { Spacing = 8, Orientation = Orientation.Horizontal };
            switch (node.ClassName) 
            {
                case "PostMention":
                    stack.Children.Add(new FontIcon { Glyph = "\uE97A", FontSize = 14 });
                    break;
            }
            stack.Children.Add(new TextBlock { Text = CleanText(node.TextContent) });
            hyperlink.Content = stack;

            var container = new InlineUIContainer();
            container.Child = hyperlink;
            
            return container;

        }

        private Inline GenerateBold(INode node)
        {
            var bold = new Bold();

            AddInlineChildren(node, bold.Inlines);

            return bold;
        }

        private Inline GenerateItalic(INode node)
        {
            var italic = new Italic();

            AddInlineChildren(node, italic.Inlines);

            return italic;
        }

        private Inline GenerateUnderline(INode node)
        {
            var underline = new Underline();

            AddInlineChildren(node, underline.Inlines);

            return underline;
        }

        private Inline GenerateStrike(INode node)
        {
            var span = new Span()
            {
                TextDecorations = TextDecorations.Strikethrough,
            };

            AddInlineChildren(node, span.Inlines);

            return span;
        }

        private Inline GenerateLineBreak()
        {
            return new LineBreak();
        }

        private Inline GenerateSpan(INode node)
        {
            var span = new Span();

            AddInlineChildren(node, span.Inlines);

            return span;
        }

        private UIElement GeneratePreformatted(IHtmlPreElement node)
        {
            var border = new Border()
            {
                Style = PreformattedBorderStyle,
            };

            if (node.FirstChild.NodeName == "CODE")
            {
                var codeBlock = GeneratePreformattedCodeBlock(node.FirstChild as IHtmlElement);
                border.Child = codeBlock;
            }
            else
            {
                var span = new Span()
                {
                    FontFamily = new FontFamily("Consolas"),
                };

                var run = new Run()
                {
                    Text = node.TextContent,
                };

                span.Inlines.Add(run);

                var textBlock = new RichTextBlock();
                var paragraph = new Paragraph();

                paragraph.Inlines.Add(span);
                textBlock.Blocks.Add(paragraph);

                border.Child = textBlock;
            }

            return border;
        }

        private UIElement GeneratePreformattedCodeBlock(IHtmlElement node)
        {
            string language = node.GetAttribute("class");
            HighlightLanguage highlightLanguage = HighlightLanguage.PlainText;
            switch (language)
            {
                case "python":
                    highlightLanguage = HighlightLanguage.Python;
                    break;
                case "javascript":
                case "js":
                case "jsx":
                    highlightLanguage = HighlightLanguage.JavaScript;
                    break;
                case "json":
                    highlightLanguage = HighlightLanguage.JSON;
                    break;
                case "cs":
                case "csharp":
                    highlightLanguage = HighlightLanguage.CSharp;
                    break;
                case "c":
                case "c++":
                case "cc":
                case "cpp":
                    highlightLanguage = HighlightLanguage.CPlusPlus;
                    break;
                case "css":
                    highlightLanguage = HighlightLanguage.CSS;
                    break;
                case "php":
                    highlightLanguage = HighlightLanguage.PHP;
                    break;
                case "ruby":
                case "rb":
                    highlightLanguage = HighlightLanguage.Ruby;
                    break;
                case "html":
                case "xml":
                case "xhtml":
                case "rss":
                    highlightLanguage = HighlightLanguage.XML;
                    break;
                case "java":
                case "jsp":
                    highlightLanguage = HighlightLanguage.Java;
                    break;
                case "sql":
                    highlightLanguage = HighlightLanguage.SQL;
                    break;
                default:
                    highlightLanguage = HighlightLanguage.CPlusPlus;
                    break;
            }

            return new CodeHighlightedTextBlock()
            {
                Code = CleanText(node.InnerHtml),
                HighlightLanguage = highlightLanguage,
            };
        }

        private Inline GenerateCode(INode node)
        {
            var span = new Span()
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(212, 157, 120, 0)),
            };

            var run = new Run()
            {
                Text = node.TextContent,
            };

            span.Inlines.Add(run);

            return span;
        }

        private Inline GenerateHeader(INode node)
        {
            var span = new Span();

            AddInlineChildren(node, span.Inlines);

            switch (node.NodeName)
            {
                case "H1":
                    span.FontSize = 32;
                    break;
                case "H2":
                    span.FontSize = 28;
                    break;
                case "H3":
                    span.FontSize = 24;
                    break;
                case "H4":
                    span.FontSize = 20;
                    break;
                case "H5":
                    span.FontSize = 18;
                    break;
                case "H6":
                    span.FontSize = 14;
                    break;
                default:
                    span.FontSize = 14;
                    break;
            }
            span.FontWeight = FontWeights.SemiBold;

            return span;
        }

        private Inline GenerateSmall(INode node)
        {
            var span = new Span()
            {
                FontSize = 12,
            };

            AddInlineChildren(node, span.Inlines);

            return span;
        }

        private Inline GenerateMark(INode node)
        {
            var span = new Span()
            {
                TextDecorations = TextDecorations.Underline,
                Foreground = new SolidColorBrush(Colors.Goldenrod),
                FontWeight = FontWeights.SemiBold,
            };

            AddInlineChildren(node, span.Inlines);

            return span;
        }

        private Inline GenerateAbbreviation(IHtmlElement node)
        {
            var span = new Span()
            {
                TextDecorations = TextDecorations.Underline,
            };

            AddInlineChildren(node, span.Inlines);

            if (node.HasAttribute("title"))
            {
                ToolTip toolTip = new ToolTip()
                {
                    Content = node.GetAttribute("title"),
                };

                ToolTipService.SetToolTip(span, toolTip);
            }

            return span;
        }

        private UIElement GenerateBlockQuote(INode node)
        {
            var border = new Border()
            {
                Style = BlockquoteBorderStyle,
                Child = GenerateDiv(node),
            };

            return border;
        }

        private Inline GenerateQuote(INode node)
        {
            var run = new Run()
            {
                Text = $"“{node.TextContent}”",
                FontStyle = FontStyle.Italic,
            };

            return run;
        }

        private Inline GenerateHorizontalRule()
        {
            var inlineContainer = new InlineUIContainer();
            var line = new Line
            {
                X1 = 0,
                Y1 = 0,
                X2 = 1000,
                Y2 = 0,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                Margin = new Thickness(0, 9.5, 0, 9.5),
            };
            inlineContainer.Child = line;

            return inlineContainer;
        }

        private UIElement GenerateIframe(IHtmlInlineFrameElement node)
        {
            var webView = new WebView2();

            if (!String.IsNullOrEmpty(node.ContentHtml))
            {
                webView.NavigateToString(node.ContentHtml);
            }
            else if (Uri.TryCreate(node.Source, UriKind.RelativeOrAbsolute, out Uri sourceUrl))
            {
                webView.NavigateToString(sourceUrl.OriginalString);
            }

            return webView;
        }

        private Inline GeneratePlainText(INode node)
        {
            var textRun = GenerateTextRun(node.TextContent);

            return textRun;
        }

        private Run GenerateTextRun(string textContent)
        {
            return new Run()
            {
                Text = CleanText(textContent),
            };
        }

        private string CleanText(string input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return String.Empty;

            string clean = WebUtility.HtmlDecode(input);
            if (clean == "\0")
                clean = "\n";
            
            return clean;
        }
    }
}
