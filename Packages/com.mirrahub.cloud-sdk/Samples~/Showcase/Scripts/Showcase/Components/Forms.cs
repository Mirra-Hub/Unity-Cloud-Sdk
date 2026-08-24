using System;
using System.Collections.Generic;
using System.Globalization;
using MirraCloud.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>
    /// Which runtime control a <see cref="FormField"/> renders as. Int/Float/Json are text inputs on
    /// purpose: the showcase needs "left blank" to stay distinguishable from 0, and it needs a place
    /// to surface a parse error instead of silently coercing what the user typed.
    /// </summary>
    public enum FormFieldKind { Text, LongText, Password, Int, Float, Bool, Choice, Json }

    /// <summary>
    /// Declarative description of one input. Views describe a write operation as a list of these
    /// instead of hand-wiring controls, so every form in the example gets the same layout, hints,
    /// required markers and inline validation.
    /// </summary>
    public sealed class FormField
    {
        public string Key;
        public string Label;
        public FormFieldKind Kind;

        /// <summary>Choices for <see cref="FormFieldKind.Choice"/>; ignored by every other kind.</summary>
        public string[] Options;

        /// <summary>Initial value, typed per kind (string / int / float / bool). Null means empty.</summary>
        public object Default;

        /// <summary>Hint rendered under the input — use it for what the API expects, not to repeat the label.</summary>
        public string Placeholder;

        public bool Required;

        public static FormField Text(string key, string label, string def = null, bool required = false)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Text, Default = def, Required = required };
        }

        public static FormField LongText(string key, string label, string def = null, bool required = false)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.LongText, Default = def, Required = required };
        }

        public static FormField Password(string key, string label, string def = null, bool required = false)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Password, Default = def, Required = required };
        }

        public static FormField Int(string key, string label, int def = 0)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Int, Default = def };
        }

        public static FormField Float(string key, string label, float def = 0f)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Float, Default = def };
        }

        public static FormField Bool(string key, string label, bool def = false)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Bool, Default = def };
        }

        public static FormField Choice(string key, string label, string[] options, string def = null)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Choice, Options = options, Default = def };
        }

        public static FormField Json(string key, string label, string def = null)
        {
            return new FormField { Key = key, Label = label, Kind = FormFieldKind.Json, Default = def };
        }

        /// <summary>Fluent hint setter (the factories stay short; every kind can carry a hint).</summary>
        public FormField WithPlaceholder(string hint)
        {
            Placeholder = hint;
            return this;
        }

        /// <summary>Fluent required flag — the numeric/choice/json factories deliberately have no such parameter.</summary>
        public FormField AsRequired(bool required = true)
        {
            Required = required;
            return this;
        }

        /// <summary>Fluent default setter for the kinds whose factory takes no typed default.</summary>
        public FormField WithDefault(object value)
        {
            Default = value;
            return this;
        }
    }

    /// <summary>
    /// Snapshot of what a form held at the moment it was read. Values are stored as raw strings and
    /// converted on access, so a caller can ask for the same key as text or as a number without the
    /// form having to know which SDK argument it feeds.
    /// </summary>
    public sealed class FormValues
    {
        private readonly Dictionary<string, string> _raw = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Stores a raw value (fluent). Used by <see cref="FormView"/>; handy for tests too.</summary>
        public FormValues Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _raw[key] = value ?? string.Empty;
            }
            return this;
        }

        /// <summary>True when the key exists and carries a non-blank value — the "was it filled in?" test.</summary>
        public bool Has(string key)
        {
            return key != null && _raw.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);
        }

        public string Text(string key)
        {
            if (key != null && _raw.TryGetValue(key, out var v) && v != null)
            {
                return v;
            }
            return string.Empty;
        }

        public int Int(string key)
        {
            return int.TryParse(Text(key).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        }

        public float Float(string key)
        {
            return float.TryParse(Text(key).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        }

        public bool Bool(string key)
        {
            var t = Text(key).Trim();
            return string.Equals(t, "true", StringComparison.OrdinalIgnoreCase) || t == "1";
        }

        public string Choice(string key)
        {
            return Text(key);
        }

        /// <summary>Parsed payload of a <see cref="FormFieldKind.Json"/> field; null when blank or unparsable.</summary>
        public JsonValue Json(string key)
        {
            var t = Text(key);
            if (string.IsNullOrWhiteSpace(t))
            {
                return null;
            }
            try
            {
                return JsonMapper.FromJson<JsonValue>(t);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] FormValues.Json('" + key + "') failed to parse: " + e.Message);
                return null;
            }
        }

        /// <summary>Keys present in the snapshot (declaration order is not preserved).</summary>
        public IEnumerable<string> Keys => _raw.Keys;
    }

    /// <summary>
    /// A stack of inputs built from <see cref="FormField"/> descriptors, with per-field inline
    /// validation. Deliberately modal-free so it can also live inside a card; <see cref="FormDialog"/>
    /// is the popup wrapper around it.
    /// </summary>
    public sealed class FormView : VisualElement
    {
        /// <summary>Raised when Enter is pressed in a single-line input (the dialog maps it to submit).</summary>
        public event Action Submitted;

        private readonly List<Row> _rows = new List<Row>();

        public FormView(IEnumerable<FormField> fields)
        {
            AddToClassList("sc-form");

            if (fields == null)
            {
                return;
            }

            foreach (var f in fields)
            {
                if (f == null || string.IsNullOrEmpty(f.Key))
                {
                    Debug.LogWarning("[Showcase] FormView: skipped a field with no key.");
                    continue;
                }
                var row = BuildRow(f);
                _rows.Add(row);
                Add(row.Root);
            }
        }

        /// <summary>Reads every control right now and returns an immutable-ish snapshot.</summary>
        public FormValues Values
        {
            get
            {
                var v = new FormValues();
                foreach (var r in _rows)
                {
                    v.Set(r.Def.Key, Normalize(r.Def.Kind, r.Read()));
                }
                return v;
            }
        }

        /// <summary>Number of inputs actually built (fields without a key are dropped).</summary>
        public int FieldCount => _rows.Count;

        /// <summary>
        /// Checks required-ness, number parsing and JSON parsing. Marks every offending field inline
        /// and returns the first message, so a caller can also toast it.
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;
            ClearErrors();

            foreach (var r in _rows)
            {
                string msg = Check(r.Def, Normalize(r.Def.Kind, r.Read()));
                if (msg == null)
                {
                    continue;
                }
                ShowError(r, msg);
                if (error == null)
                {
                    error = msg;
                }
            }
            return error == null;
        }

        /// <summary>Hides every inline error (called by <see cref="Validate"/> before re-checking).</summary>
        public void ClearErrors()
        {
            foreach (var r in _rows)
            {
                r.Root.RemoveFromClassList("sc-form-field--invalid");
                r.Error.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Enables/disables the inputs (e.g. while a request is in flight). Named with a 2 because
        /// VisualElement.SetEnabled is non-virtual and already taken.
        /// </summary>
        public void SetEnabled2(bool enabled)
        {
            foreach (var r in _rows)
            {
                r.Control.SetEnabled(enabled);
            }
        }

        /// <summary>Focuses the first input; false when the form has none (caller focuses something else).</summary>
        public bool FocusFirst()
        {
            if (_rows.Count == 0)
            {
                return false;
            }
            _rows[0].Control.Focus();
            return true;
        }

        private Row BuildRow(FormField f)
        {
            var root = new VisualElement();
            root.AddToClassList("sc-form-field");

            string label = f.Required ? Name(f) + " *" : Name(f);
            VisualElement control;
            Func<string> read;

            switch (f.Kind)
            {
                case FormFieldKind.LongText:
                case FormFieldKind.Json:
                {
                    var tf = new TextField(label) { multiline = true, value = DefaultText(f) };
                    tf.AddToClassList("sc-field");
                    tf.AddToClassList("sc-field--multiline");
                    if (f.Kind == FormFieldKind.Json)
                    {
                        tf.AddToClassList("sc-field--code");
                    }
                    control = tf;
                    read = () => tf.value;
                    break;
                }
                case FormFieldKind.Bool:
                {
                    var tg = new Toggle(label) { value = DefaultBool(f) };
                    tg.AddToClassList("sc-field");
                    tg.AddToClassList("sc-field--toggle");
                    control = tg;
                    read = () => tg.value ? "true" : "false";
                    break;
                }
                case FormFieldKind.Choice:
                {
                    var options = new List<string>();
                    if (f.Options != null)
                    {
                        foreach (var o in f.Options)
                        {
                            options.Add(o ?? string.Empty);
                        }
                    }

                    DropdownField dd;
                    if (options.Count > 0)
                    {
                        int idx = options.IndexOf(f.Default as string ?? string.Empty);
                        dd = new DropdownField(label, options, idx < 0 ? 0 : idx);
                    }
                    else
                    {
                        // DropdownField's index setter indexes into choices — never touch it while empty.
                        dd = new DropdownField(label);
                        dd.choices = options;
                        Debug.LogWarning("[Showcase] FormView: choice field '" + f.Key + "' has no options.");
                    }
                    dd.AddToClassList("sc-field");
                    dd.AddToClassList("sc-field--choice");
                    control = dd;
                    read = () => dd.value ?? string.Empty;
                    break;
                }
                default:
                {
                    var tf = new TextField(label)
                    {
                        isPasswordField = f.Kind == FormFieldKind.Password,
                        value = DefaultText(f),
                    };
                    tf.AddToClassList("sc-field");
                    if (f.Kind == FormFieldKind.Int || f.Kind == FormFieldKind.Float)
                    {
                        tf.AddToClassList("sc-field--num");
                    }
                    HookEnter(tf);
                    control = tf;
                    read = () => tf.value;
                    break;
                }
            }

            root.Add(control);

            if (!string.IsNullOrEmpty(f.Placeholder))
            {
                var hint = new Label(f.Placeholder);
                hint.enableRichText = false;
                hint.AddToClassList("sc-form-field__hint");
                root.Add(hint);
            }

            var error = new VisualElement();
            error.AddToClassList("sc-form-field__error");
            error.style.display = DisplayStyle.None;
            var glyph = new Label(LucideIcon.TriangleAlert);
            glyph.AddToClassList("sc-form-field__error-glyph");
            glyph.AddToClassList("sc-icon");
            var errorText = new Label();
            errorText.enableRichText = false;
            errorText.AddToClassList("sc-form-field__error-msg");
            error.Add(glyph);
            error.Add(errorText);
            root.Add(error);

            return new Row
            {
                Def = f,
                Root = root,
                Control = control,
                Error = error,
                ErrorText = errorText,
                Read = read,
            };
        }

        private void HookEnter(TextField field)
        {
            // Only the keyCode form is handled: UITK also sends a character-only KeyDownEvent for
            // Return, and reacting to both would submit twice.
            field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }
                e.StopPropagation();
                Submitted?.Invoke();
            });
        }

        private static void ShowError(Row r, string message)
        {
            r.Root.AddToClassList("sc-form-field--invalid");
            r.ErrorText.text = message;
            r.Error.style.display = DisplayStyle.Flex;
        }

        /// <summary>Null when the value is acceptable, otherwise the message to show under the field.</summary>
        private static string Check(FormField f, string value)
        {
            if (f.Kind == FormFieldKind.Bool)
            {
                return null;
            }

            string name = Name(f);
            if (string.IsNullOrWhiteSpace(value))
            {
                return f.Required ? name + " is required" : null;
            }

            switch (f.Kind)
            {
                case FormFieldKind.Int:
                    if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        return name + " must be a whole number";
                    }
                    break;
                case FormFieldKind.Float:
                    if (!float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        return name + " must be a number (use a dot for decimals)";
                    }
                    break;
                case FormFieldKind.Json:
                    string jsonError = JsonError(value);
                    if (jsonError != null)
                    {
                        return name + ": " + jsonError;
                    }
                    break;
            }
            return null;
        }

        /// <summary>Null when the text parses; the SDK's own parser is used so errors match the runtime.</summary>
        private static string JsonError(string text)
        {
            try
            {
                JsonMapper.FromJson<JsonValue>(text);
                return null;
            }
            catch (InvalidJsonException e)
            {
                return "invalid JSON — " + Fmt.Truncate(e.Message, 80);
            }
            catch (Exception e)
            {
                // Malformed input can also surface as FormatException / EndOfStreamException.
                return "invalid JSON — " + Fmt.Truncate(e.Message, 80);
            }
        }

        /// <summary>Multi-line payloads keep their whitespace; single-line inputs are trimmed for the caller.</summary>
        private static string Normalize(FormFieldKind kind, string raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }
            switch (kind)
            {
                case FormFieldKind.LongText:
                case FormFieldKind.Json:
                case FormFieldKind.Password:
                    return raw;
                default:
                    return raw.Trim();
            }
        }

        private static string Name(FormField f)
        {
            return string.IsNullOrEmpty(f.Label) ? f.Key : f.Label;
        }

        /// <summary>Default rendered as editable text — invariant culture, so "1.5" never becomes "1,5".</summary>
        private static string DefaultText(FormField f)
        {
            object d = f.Default;
            if (d == null)
            {
                return string.Empty;
            }
            if (d is string)
            {
                return (string)d;
            }
            if (d is bool)
            {
                return (bool)d ? "true" : "false";
            }
            if (d is int)
            {
                return ((int)d).ToString(CultureInfo.InvariantCulture);
            }
            if (d is float)
            {
                return ((float)d).ToString(CultureInfo.InvariantCulture);
            }
            if (d is double)
            {
                return ((double)d).ToString(CultureInfo.InvariantCulture);
            }
            if (d is long)
            {
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            }
            return Convert.ToString(d, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool DefaultBool(FormField f)
        {
            return f.Default is bool && (bool)f.Default;
        }

        private sealed class Row
        {
            public FormField Def;
            public VisualElement Root;
            public VisualElement Control;
            public VisualElement Error;
            public Label ErrorText;
            public Func<string> Read;
        }
    }

    /// <summary>
    /// Shared modal plumbing: Popup only closes on the scrim or its close button, so dialogs wire
    /// Escape and initial focus themselves (key events only reach elements on the focus path).
    /// </summary>
    internal static class DialogChrome
    {
        /// <summary>Closes the popup on Escape. TrickleDown so a focused TextField can't swallow it.</summary>
        public static void HookEscape(VisualElement content, Popup popup)
        {
            content.focusable = true;
            content.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape)
                {
                    return;
                }
                e.StopPropagation();
                popup.Close();
            }, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Focuses <paramref name="preferred"/> (or the content root) once the dialog is in a panel —
        /// focusing during construction is a no-op because the element has no panel yet.
        /// </summary>
        public static void FocusOnAttach(VisualElement content, Func<bool> preferred)
        {
            content.RegisterCallback<AttachToPanelEvent>(_ =>
                content.schedule.Execute(() =>
                {
                    if (preferred == null || !preferred())
                    {
                        content.Focus();
                    }
                }).StartingIn(0));
        }

        /// <summary>Dialog footer button (not <c>sc-btn--block</c> — the actions row spaces them).</summary>
        public static Button Btn(string text, string toneClass, Action onClick)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.AddToClassList("sc-btn");
            if (!string.IsNullOrEmpty(toneClass))
            {
                b.AddToClassList(toneClass);
            }
            return b;
        }
    }

    /// <summary>
    /// Opens a <see cref="FormView"/> in a modal with Cancel/Submit. Submit validates first and keeps
    /// the dialog open on failure; on success the dialog closes *before* the callback runs, so the
    /// callback is free to toast or open another popup.
    /// </summary>
    public static class FormDialog
    {
        public static void Open(Popup popup, string title, IEnumerable<FormField> fields,
                                string submitText, Action<FormValues> onSubmit,
                                bool destructive = false)
        {
            if (popup == null)
            {
                Debug.LogWarning("[Showcase] FormDialog.Open: no popup host — '" + title + "' not shown.");
                return;
            }

            var root = new VisualElement();
            root.AddToClassList("sc-form-dialog");

            var form = new FormView(fields);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sc-form-dialog__scroll");
            scroll.Add(form);
            root.Add(scroll);

            Action submit = () =>
            {
                if (!form.Validate(out _))
                {
                    return;
                }
                var values = form.Values;
                popup.Close();
                onSubmit?.Invoke(values);
            };
            form.Submitted += () => submit();

            var actions = new VisualElement();
            actions.AddToClassList("sc-form__actions");
            actions.Add(DialogChrome.Btn("Cancel", null, popup.Close));
            actions.Add(DialogChrome.Btn(
                string.IsNullOrEmpty(submitText) ? "Submit" : submitText,
                destructive ? "sc-btn--danger" : "sc-btn--primary",
                () => submit()));
            root.Add(actions);

            DialogChrome.HookEscape(root, popup);
            DialogChrome.FocusOnAttach(root, form.FocusFirst);

            popup.Open(root, title);
        }
    }

    /// <summary>
    /// Yes/no modal for irreversible actions. Pass <c>requireTyped</c> for real deletions: the confirm
    /// button stays disabled until the user retypes that exact string, which makes an accidental
    /// double-click impossible.
    /// </summary>
    public static class ConfirmDialog
    {
        public static void Open(Popup popup, string title, string message, string confirmText,
                                Action onConfirm, string requireTyped = null, bool destructive = true)
        {
            if (popup == null)
            {
                Debug.LogWarning("[Showcase] ConfirmDialog.Open: no popup host — '" + title + "' not shown.");
                return;
            }

            var root = new VisualElement();
            root.AddToClassList("sc-form-dialog");

            var msg = new Label(message ?? string.Empty);
            msg.enableRichText = false;
            msg.AddToClassList("sc-confirm__msg");
            root.Add(msg);

            bool gated = !string.IsNullOrEmpty(requireTyped);
            TextField typed = null;
            if (gated)
            {
                root.Add(GateHint(requireTyped));
                typed = new TextField();
                typed.AddToClassList("sc-field");
                typed.AddToClassList("sc-confirm__input");
                root.Add(typed);
            }

            Action fire = () =>
            {
                if (gated && !string.Equals(typed.value, requireTyped, StringComparison.Ordinal))
                {
                    return;
                }
                popup.Close();
                onConfirm?.Invoke();
            };

            var confirm = DialogChrome.Btn(
                string.IsNullOrEmpty(confirmText) ? "Confirm" : confirmText,
                destructive ? "sc-btn--danger" : "sc-btn--primary",
                () => fire());

            if (gated)
            {
                confirm.SetEnabled(false);
                typed.RegisterValueChangedCallback(e =>
                    confirm.SetEnabled(string.Equals(e.newValue, requireTyped, StringComparison.Ordinal)));
                typed.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter)
                    {
                        return;
                    }
                    e.StopPropagation();
                    fire();
                });
            }

            var actions = new VisualElement();
            actions.AddToClassList("sc-form__actions");
            actions.Add(DialogChrome.Btn("Cancel", null, popup.Close));
            actions.Add(confirm);
            root.Add(actions);

            DialogChrome.HookEscape(root, popup);
            DialogChrome.FocusOnAttach(root, () =>
            {
                if (typed == null)
                {
                    return false;
                }
                typed.Focus();
                return true;
            });

            popup.Open(root, title);
        }

        /// <summary>"Type &lt;token&gt; to confirm" as three labels — rich text stays off for user-supplied keys.</summary>
        private static VisualElement GateHint(string requireTyped)
        {
            var row = new VisualElement();
            row.AddToClassList("sc-confirm__gate");

            var lead = new Label("Type");
            lead.AddToClassList("sc-confirm__gate-text");
            var code = new Label(requireTyped);
            code.enableRichText = false;
            code.AddToClassList("sc-confirm__gate-code");
            var tail = new Label("to confirm");
            tail.AddToClassList("sc-confirm__gate-text");

            row.Add(lead);
            row.Add(code);
            row.Add(tail);
            return row;
        }
    }
}
