using UnityEngine.UIElements;

namespace MirraCloud.Example.Showcase
{
    /// <summary>A list row: lead slot (avatar/icon) + title/subtitle + trailing slot.</summary>
    public sealed class ListRow : VisualElement
    {
        public readonly VisualElement Lead;
        public readonly Label Title;
        public readonly Label Subtitle;
        public readonly VisualElement Trailing;

        public ListRow()
        {
            AddToClassList("sc-list-row");

            Lead = new VisualElement();
            Lead.AddToClassList("sc-list-row__lead");
            Add(Lead);

            var texts = new VisualElement();
            texts.AddToClassList("sc-list-row__texts");
            Title = new Label();
            Title.AddToClassList("sc-list-row__title");
            Subtitle = new Label();
            Subtitle.AddToClassList("sc-list-row__subtitle");
            texts.Add(Title);
            texts.Add(Subtitle);
            Add(texts);

            Trailing = new VisualElement();
            Trailing.AddToClassList("sc-list-row__trailing");
            Add(Trailing);
        }

        public ListRow SetTitle(string text)
        {
            Title.text = text;
            return this;
        }

        public ListRow SetSubtitle(string text)
        {
            Subtitle.text = text;
            Subtitle.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            return this;
        }

        public ListRow SetLead(VisualElement element)
        {
            Lead.Clear();
            if (element != null)
            {
                Lead.Add(element);
            }
            return this;
        }

        public ListRow SetTrailing(VisualElement element)
        {
            Trailing.Clear();
            if (element != null)
            {
                Trailing.Add(element);
            }
            return this;
        }
    }
}
