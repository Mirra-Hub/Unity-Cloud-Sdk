using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// One "this is the code behind this screen" entry for the <c>&lt;/&gt;</c> drawer: the C# a view
    /// actually runs, so a reader can copy it straight into their own project. Keep
    /// <see cref="Snippet"/> byte-for-byte in sync with the call site — a stale snippet is worse
    /// than none.
    /// </summary>
    public sealed class SdkCall
    {
        public string Title;
        public string Snippet;
        public string Note;

        public SdkCall(string title, string snippet, string note = null)
        {
            Title = title;
            Snippet = snippet;
            Note = note;
        }
    }

    /// <summary>
    /// Renders the code drawer: every SDK call a screen makes (with a copy button), followed by the
    /// live <see cref="RequestLog"/> so the snippet and the HTTP traffic it produced sit side by side.
    /// </summary>
    public static class SdkCallDrawer
    {
        public static VisualElement Build(IEnumerable<SdkCall> calls, RequestLog log)
        {
            var root = new VisualElement();
            root.AddToClassList("sc-callbook");

            int shown = 0;
            if (calls != null)
            {
                foreach (var call in calls)
                {
                    if (call == null)
                    {
                        continue;
                    }
                    root.Add(BuildCall(call));
                    shown++;
                }
            }

            if (shown == 0)
            {
                root.Add(EmptyState.Build(LucideIcon.Code, "No SDK calls described for this screen"));
            }

            if (log != null)
            {
                var panel = log.BuildPanel();
                panel.style.marginTop = 6f;
                root.Add(panel);
            }

            return root;
        }

        /// <summary>A read-only code block. Rich text is off — snippets contain generics like
        /// <c>RestApiResult&lt;T&gt;</c>, which UITK would otherwise eat as markup.</summary>
        public static VisualElement CodeBlock(string code)
        {
            var l = new Label(code ?? string.Empty);
            l.enableRichText = false;
            l.AddToClassList("sc-code");
            return l;
        }

        private static VisualElement BuildCall(SdkCall call)
        {
            var box = new VisualElement();
            box.AddToClassList("sc-callbook__call");

            var head = new VisualElement();
            head.AddToClassList("sc-callbook__head");

            var glyph = new Label(LucideIcon.Code);
            glyph.AddToClassList("sc-callbook__glyph");
            glyph.AddToClassList("sc-icon");
            head.Add(glyph);

            var title = new Label(string.IsNullOrEmpty(call.Title) ? "SDK call" : call.Title);
            title.enableRichText = false;
            title.AddToClassList("sc-callbook__title");
            head.Add(title);

            head.Add(new CopyButton(call.Snippet, null, "Copy"));
            box.Add(head);

            box.Add(CodeBlock(call.Snippet));

            if (!string.IsNullOrEmpty(call.Note))
            {
                var note = new Label(call.Note);
                note.enableRichText = false;
                note.AddToClassList("sc-callbook__note");
                box.Add(note);
            }

            return box;
        }
    }
}
