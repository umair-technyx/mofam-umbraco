using Mofam.Application.Abstractions;
using Mofam.Application.Helpers;
using Mofam.Domain.Models.Dtos;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Strings;

namespace Mofam.Application.Mapping;

public sealed class PropertyValueMapper(IMediaUrlBuilder mediaUrlBuilder) : IPropertyValueMapper
{
    public bool TryMapLeaf(object? value, string? culture, out object? mapped)
    {
        switch (value)
        {
            case null:
                mapped = null;
                return true;

            // Text. If an editor pasted SVG code into a text field, it is cleaned to one tidy line.
            case string text:
                mapped = SvgCleaner.Clean(text);
                return true;

            case bool or int or long or double or decimal or Guid or DateTime or DateTimeOffset:
                mapped = value;
                return true;

            // Color picker: send the colour as a CSS value, for example "#FEEAC2".
            case ColorPickerValueConverter.PickedColor pickedColor:
                mapped = ToCssColor(pickedColor.Color);
                return true;

            // Rich text: emit the HTML rather than the wrapper type.
            case IHtmlEncodedString html:
                mapped = html.ToHtmlString();
                return true;

            // URL pickers, before the generic content branches — otherwise a Link
            // serialises as "Umbraco.Cms.Core.Models.Link".
            case Link link:
                mapped = LinkMapper.Map(link);
                return true;

            case IEnumerable<Link> links:
                mapped = LinkMapper.MapMany(links);
                return true;

            // Media before content, otherwise it becomes a property bag with no URL.
            case IPublishedContent media when media.ItemType == PublishedItemType.Media:
                mapped = mediaUrlBuilder.Build(media, culture);
                return true;

            case IEnumerable<IPublishedContent> mediaList when mediaUrlBuilder.IsMedia(mediaList):
                mapped = mediaUrlBuilder.BuildMany(mediaList, culture);
                return true;

            case IEnumerable<string> strings:
                mapped = strings.ToList();
                return true;

            default:
                // Blocks and content nodes need caller-specific recursion.
                mapped = null;
                return false;
        }
    }

    private static string? ToCssColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;

        color = color.Trim();

        return !color.StartsWith('#') && color.All(Uri.IsHexDigit) && color.Length is 3 or 4 or 6 or 8
            ? "#" + color
            : color;
    }

    public object? Raw(IPublishedElement element, string alias, string? culture)
    {
        var property = element.GetProperty(alias);
        return property is null ? null : property.GetValue(culture) ?? property.GetValue(null);
    }

    public string? Text(IPublishedElement element, string alias, string? culture) =>
        Raw(element, alias, culture) switch
        {
            null => null,
            IHtmlEncodedString html => html.ToHtmlString(),
            var v => v.ToString(),
        };

    public MediaDto? Media(IPublishedElement element, string alias, string? culture) =>
        Raw(element, alias, culture) switch
        {
            IEnumerable<IPublishedContent> many => mediaUrlBuilder.BuildMany(many, culture).FirstOrDefault(),
            IPublishedContent one => mediaUrlBuilder.Build(one, culture),
            _ => null,
        };

    public IReadOnlyList<LinkDto> Links(IPublishedElement element, string alias, string? culture) =>
        Raw(element, alias, culture) switch
        {
            IEnumerable<Link> many => LinkMapper.MapMany(many),
            Link one => LinkMapper.Map(one) is { } dto ? [dto] : [],
            _ => [],
        };

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> BlockListItems(IPublishedElement element, string alias, string? culture) =>
        Raw(element, alias, culture) is BlockListModel blocks
            ? blocks
                .Select(block => (IReadOnlyDictionary<string, object?>)block.Content.Properties.ToDictionary(
                    property => property.Alias,
                    property =>
                    {
                        var value = property.GetValue(culture) ?? property.GetValue(null);
                        return TryMapLeaf(value, culture, out var mapped) ? mapped : value?.ToString();
                    }))
                .ToList()
            : [];

    public string? Color(IPublishedElement element, string alias, string? culture) =>
        Raw(element, alias, culture) switch
        {
            ColorPickerValueConverter.PickedColor picked => ToCssColor(picked.Color),
            string text => ToCssColor(text),
            _ => null,
        };
}
