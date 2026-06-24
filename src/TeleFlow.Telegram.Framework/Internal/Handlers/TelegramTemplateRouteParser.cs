using System.Text;
using System.Text.RegularExpressions;

namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramTemplateRouteParser
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:(?<nameOptional>\?)|:(?<constraint>[A-Za-z][A-Za-z0-9_]*)(?<constraintOptional>\?)?)?\}",
        RegexOptions.CultureInvariant);

    public static IReadOnlyList<TelegramRouteValueDescriptor> GetRouteValues(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var values = new List<TelegramRouteValueDescriptor>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            if (match.Index != position)
            {
                EnsureNoUnparsedPlaceholder(template, position, match.Index);
            }

            var placeholder = GetPlaceholder(template, match);
            var name = placeholder.Name;

            if (!names.Add(name))
            {
                throw new InvalidOperationException($"Telegram route template '{template}' contains duplicate placeholder '{name}'.");
            }

            values.Add(new TelegramRouteValueDescriptor(
                name,
                GetConstraintType(template, placeholder.Constraint),
                placeholder.IsOptional));
            position = match.Index + match.Length;
        }

        EnsureNoUnparsedPlaceholder(template, position, template.Length);

        return values;
    }

    public static Regex BuildRegex(
        string template,
        bool ignoreCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var builder = new StringBuilder("^");
        var position = 0;

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            if (match.Index != position)
            {
                EnsureNoUnparsedPlaceholder(template, position, match.Index);
            }

            var literal = template[position..match.Index];
            var placeholder = GetPlaceholder(template, match);

            if (placeholder.IsOptional)
            {
                var optionalPrefixLength = GetTrailingWhitespaceLength(literal);
                var requiredLiteral = literal[..(literal.Length - optionalPrefixLength)];
                var optionalPrefix = literal[(literal.Length - optionalPrefixLength)..];

                builder.Append(Regex.Escape(requiredLiteral));
                builder.Append("(?:");
                builder.Append(Regex.Escape(optionalPrefix));
                AppendRouteValueGroup(builder, template, placeholder);
                builder.Append(")?");
            }
            else
            {
                builder.Append(Regex.Escape(literal));
                AppendRouteValueGroup(builder, template, placeholder);
            }

            position = match.Index + match.Length;
        }

        EnsureNoUnparsedPlaceholder(template, position, template.Length);
        builder.Append(Regex.Escape(template[position..]));
        builder.Append('$');

        return new Regex(
            builder.ToString(),
            GetRegexOptions(ignoreCase));
    }

    public static int GetSpecificity(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);

        var score = 0;
        var position = 0;

        foreach (Match match in PlaceholderRegex.Matches(template))
        {
            if (match.Index != position)
            {
                EnsureNoUnparsedPlaceholder(template, position, match.Index);
            }

            score += GetLiteralSpecificity(template[position..match.Index]);

            var placeholder = GetPlaceholder(template, match);
            score += GetConstraintSpecificity(template, placeholder.Constraint);
            score += placeholder.IsOptional ? 0 : 5;

            position = match.Index + match.Length;
        }

        EnsureNoUnparsedPlaceholder(template, position, template.Length);
        score += GetLiteralSpecificity(template[position..]);

        return score;
    }

    private static Placeholder GetPlaceholder(string template, Match match)
    {
        var name = match.Groups["name"].Value;
        var hasNameOptional = match.Groups["nameOptional"].Success;
        var hasConstraintOptional = match.Groups["constraintOptional"].Success;

        var constraint = match.Groups["constraint"].Success
            ? match.Groups["constraint"].Value
            : "string";

        return new Placeholder(name, constraint, hasNameOptional || hasConstraintOptional);
    }

    private static void AppendRouteValueGroup(
        StringBuilder builder,
        string template,
        Placeholder placeholder)
    {
        builder.Append("(?<");
        builder.Append(placeholder.Name);
        builder.Append('>');
        builder.Append(GetConstraintPattern(template, placeholder.Constraint));
        builder.Append(')');
    }

    private static int GetTrailingWhitespaceLength(string value)
    {
        var length = 0;

        for (var index = value.Length - 1; index >= 0 && char.IsWhiteSpace(value[index]); index--)
        {
            length++;
        }

        return length;
    }

    private static int GetLiteralSpecificity(string value)
    {
        return value.Count(static character => !char.IsWhiteSpace(character)) * 1000;
    }

    private static void EnsureNoUnparsedPlaceholder(
        string template,
        int start,
        int end)
    {
        var segment = template[start..end];

        if (segment.Contains('{', StringComparison.Ordinal) ||
            segment.Contains('}', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Telegram route template '{template}' contains an invalid placeholder.");
        }
    }

    private static Type GetConstraintType(string template, string constraint)
    {
        return constraint switch
        {
            "string" => typeof(string),
            "int" => typeof(int),
            "long" => typeof(long),
            _ => throw new InvalidOperationException(
                $"Telegram route template '{template}' uses unsupported placeholder constraint '{constraint}'.")
        };
    }

    private static string GetConstraintPattern(string template, string constraint)
    {
        return constraint switch
        {
            "string" => ".+?",
            "int" => "-?\\d+",
            "long" => "-?\\d+",
            _ => throw new InvalidOperationException(
                $"Telegram route template '{template}' uses unsupported placeholder constraint '{constraint}'.")
        };
    }

    private static int GetConstraintSpecificity(string template, string constraint)
    {
        return constraint switch
        {
            "string" => 10,
            "int" => 100,
            "long" => 100,
            _ => throw new InvalidOperationException(
                $"Telegram route template '{template}' uses unsupported placeholder constraint '{constraint}'.")
        };
    }

    public static RegexOptions GetRegexOptions(bool ignoreCase)
    {
        return RegexOptions.CultureInvariant |
               (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
    }

    private readonly record struct Placeholder(
        string Name,
        string Constraint,
        bool IsOptional);
}
