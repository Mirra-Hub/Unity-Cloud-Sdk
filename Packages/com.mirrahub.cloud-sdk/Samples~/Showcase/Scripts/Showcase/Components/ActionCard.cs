using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Result of one <see cref="ActionCard"/> run. Failure is a value, not an exception — it mirrors
    /// how the SDK reports HTTP errors, so a view can map <c>RestApiResult</c> straight onto it.
    /// </summary>
    public sealed class ActionOutcome
    {
        public bool Ok;
        public string Message;

        /// <summary>Optional rendered payload (table, JSON viewer, chips) shown under the message.</summary>
        public VisualElement Detail;

        public static ActionOutcome Success(string message, VisualElement detail = null)
        {
            return new ActionOutcome { Ok = true, Message = message, Detail = detail };
        }

        public static ActionOutcome Failure(string message)
        {
            return new ActionOutcome { Ok = false, Message = message };
        }
    }

    /// <summary>
    /// One invocable SDK call presented as a card: description, input form, the C# snippet that
    /// performs it, and a run button whose result lands inline instead of in a toast. The whole
    /// point is that a reader can match what they clicked to the code they would write.
    /// </summary>
    public sealed class ActionCard : VisualElement
    {
        private const string BusyText = "Working…";

        private readonly string _title;
        private readonly VisualElement _formSlot;
        private readonly VisualElement _snippetSlot;
        private readonly VisualElement _footer;
        private readonly VisualElement _resultSlot;

        private FormView _form;
        private FormView _emptyValues;
        private Button _run;
        private string _runText = "Run";
        private Func<FormValues, Task<ActionOutcome>> _handler;
        private bool _busy;

        public ActionCard(string title, string description, string glyph)
        {
            _title = title ?? string.Empty;

            AddToClassList("sc-action");

            var head = new VisualElement();
            head.AddToClassList("sc-action__head");

            if (!string.IsNullOrEmpty(glyph))
            {
                var g = new Label(glyph);
                g.AddToClassList("sc-action__glyph");
                g.AddToClassList("sc-icon");
                head.Add(g);
            }

            var texts = new VisualElement();
            texts.AddToClassList("sc-action__texts");

            var titleLabel = new Label(_title);
            titleLabel.enableRichText = false;
            titleLabel.AddToClassList("sc-action__title");
            texts.Add(titleLabel);

            var descLabel = new Label(description ?? string.Empty);
            descLabel.enableRichText = false;
            descLabel.AddToClassList("sc-action__desc");
            descLabel.style.display = string.IsNullOrEmpty(description) ? DisplayStyle.None : DisplayStyle.Flex;
            texts.Add(descLabel);

            head.Add(texts);
            Add(head);

            _formSlot = new VisualElement();
            _formSlot.AddToClassList("sc-action__form-slot");
            _formSlot.style.display = DisplayStyle.None;
            Add(_formSlot);

            _snippetSlot = new VisualElement();
            _snippetSlot.AddToClassList("sc-action__snippet");
            _snippetSlot.style.display = DisplayStyle.None;
            Add(_snippetSlot);

            _footer = new VisualElement();
            _footer.AddToClassList("sc-action__footer");
            _footer.style.display = DisplayStyle.None;
            Add(_footer);

            _resultSlot = new VisualElement();
            _resultSlot.AddToClassList("sc-action__result-slot");
            _resultSlot.style.display = DisplayStyle.None;
            Add(_resultSlot);
        }

        /// <summary>Declares the inputs of the call; their values are handed to the run delegate.</summary>
        public ActionCard WithFields(params FormField[] fields)
        {
            _formSlot.Clear();

            if (fields == null || fields.Length == 0)
            {
                _form = null;
                _formSlot.style.display = DisplayStyle.None;
                return this;
            }

            _form = new FormView(fields);
            _form.AddToClassList("sc-action__form");
            _formSlot.Add(_form);
            _formSlot.style.display = DisplayStyle.Flex;
            return this;
        }

        /// <summary>Attaches the collapsed C# call the card performs (copyable, folded by default).</summary>
        public ActionCard WithSnippet(string csharpSnippet)
        {
            _snippetSlot.Clear();

            if (string.IsNullOrEmpty(csharpSnippet))
            {
                _snippetSlot.style.display = DisplayStyle.None;
                return this;
            }

            var head = new VisualElement();
            head.AddToClassList("sc-action__snip-head");

            var chevron = new Label(LucideIcon.ChevronRight);
            chevron.AddToClassList("sc-action__snip-chev");
            chevron.AddToClassList("sc-icon");
            head.Add(chevron);

            var codeGlyph = new Label(LucideIcon.Code);
            codeGlyph.AddToClassList("sc-action__snip-glyph");
            codeGlyph.AddToClassList("sc-icon");
            head.Add(codeGlyph);

            var caption = new Label("C# snippet");
            caption.AddToClassList("sc-action__snip-title");
            head.Add(caption);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            head.Add(spacer);

            // CopyButton stops the click, so it does not also toggle the fold.
            head.Add(new CopyButton(csharpSnippet));

            var body = new ScrollView(ScrollViewMode.Horizontal);
            body.AddToClassList("sc-action__snip-body");
            body.style.display = DisplayStyle.None;

            var code = new Label(csharpSnippet);
            code.enableRichText = false;
            code.AddToClassList("sc-action__snip-code");
            code.style.whiteSpace = WhiteSpace.NoWrap;
            body.Add(code);

            bool open = false;
            head.RegisterCallback<ClickEvent>(_ =>
            {
                open = !open;
                chevron.text = open ? LucideIcon.ChevronDown : LucideIcon.ChevronRight;
                body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            });

            _snippetSlot.Add(head);
            _snippetSlot.Add(body);
            _snippetSlot.style.display = DisplayStyle.Flex;
            return this;
        }

        /// <summary>Wires the run button. Calling it again replaces the previous button.</summary>
        public ActionCard OnRun(string buttonText, Func<FormValues, Task<ActionOutcome>> run, bool destructive = false)
        {
            _handler = run;
            _runText = string.IsNullOrEmpty(buttonText) ? "Run" : buttonText;

            _footer.Clear();
            _run = new Button(Execute) { text = _runText };
            _run.AddToClassList("sc-btn");
            _run.AddToClassList(destructive ? "sc-btn--danger" : "sc-btn--primary");
            _run.AddToClassList("sc-action__run");
            _footer.Add(_run);
            _footer.style.display = DisplayStyle.Flex;
            return this;
        }

        private async void Execute()
        {
            if (_busy || _handler == null || _run == null)
            {
                return;
            }

            if (_form != null && !_form.Validate(out string error))
            {
                ShowResult(ActionOutcome.Failure(string.IsNullOrEmpty(error) ? "Check the fields above" : error));
                return;
            }

            _busy = true;
            _run.SetEnabled(false);
            _run.text = BusyText;
            _form?.SetEnabled2(false);
            ClearResult();

            ActionOutcome outcome;
            try
            {
                var task = _handler(Values());
                outcome = task == null ? null : await task;
                if (outcome == null)
                {
                    outcome = ActionOutcome.Failure("Action produced no result");
                }
            }
            catch (Exception e)
            {
                // A throwing action must never take the screen down with it.
                Debug.LogWarning("[Showcase] Action '" + _title + "' threw: " + e);
                outcome = ActionOutcome.Failure(string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message);
            }

            _busy = false;
            _run.SetEnabled(true);
            _run.text = _runText;
            _form?.SetEnabled2(true);
            ShowResult(outcome);
        }

        /// <summary>Values of the card's form, or an empty (never attached) one for field-less actions.</summary>
        private FormValues Values()
        {
            if (_form != null)
            {
                return _form.Values;
            }
            if (_emptyValues == null)
            {
                _emptyValues = new FormView(Array.Empty<FormField>());
            }
            return _emptyValues.Values;
        }

        private void ShowResult(ActionOutcome outcome)
        {
            _resultSlot.Clear();

            var plate = new VisualElement();
            plate.AddToClassList("sc-action__result");
            plate.AddToClassList(outcome.Ok ? "sc-action__result--ok" : "sc-action__result--bad");

            var line = new VisualElement();
            line.AddToClassList("sc-action__result-line");

            var glyph = new Label(outcome.Ok ? LucideIcon.CircleCheck : LucideIcon.CircleX);
            glyph.AddToClassList("sc-action__result-glyph");
            glyph.AddToClassList("sc-icon");
            line.Add(glyph);

            var message = new Label(string.IsNullOrEmpty(outcome.Message) ? (outcome.Ok ? "Done" : "Failed") : outcome.Message);
            message.enableRichText = false;
            message.AddToClassList("sc-action__result-msg");
            line.Add(message);

            plate.Add(line);

            if (outcome.Detail != null)
            {
                var detail = new VisualElement();
                detail.AddToClassList("sc-action__result-detail");
                detail.Add(outcome.Detail);
                plate.Add(detail);
            }

            _resultSlot.Add(plate);
            _resultSlot.style.display = DisplayStyle.Flex;
        }

        private void ClearResult()
        {
            _resultSlot.Clear();
            _resultSlot.style.display = DisplayStyle.None;
        }
    }
}
