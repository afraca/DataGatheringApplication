using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace DataGatheringApplication
{
    // We like named string formatting, thank you Munir Husseini, 2014
    // Property of http://mhusseini.wordpress.com/2014/05/03/fast-named-formats-in-c/ 

    public static class NamedFormat
    {
        private static readonly ConcurrentDictionary<string, object> PrecompiledExpressions =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex RegexFormatArgs =
            new Regex(@"([^{]|^){(\w+)}([^}]|$)|([^{]|^){(\w+)\:(.+)}([^}]|$)", RegexOptions.Compiled);

        public static string Format<T>(string pattern, T item)
        {
            // If we already have a compiled expression, just execute it.
            object o;
            if (PrecompiledExpressions.TryGetValue(pattern, out o))
            {
                return ((Func<T, string>) o)(item);
            }

            // Convert named format into regular format and return 
            // a list of the named arguments in order of appearance.
            string replacedPattern;
            var arguments = ParsePattern(pattern, out replacedPattern);
            // We'll be using the String.Format method to actually perform the formating.
            var formatMethod = typeof (string).GetMethod("Format", new[] {typeof (string), typeof (object[])});

            // Now, construct code with Linq Expressions...

            // The constant that contains the format string:
            var patternExpression = Expression.Constant(replacedPattern, typeof (string));
            // The input object:
            var parameterExpression = Expression.Parameter(typeof (T));
            // An array containing a call to a property getter for each named argument :
            var argumentArrayElements =
                arguments.Select(
                    argument =>
                        Expression.Convert(Expression.PropertyOrField(parameterExpression, argument), typeof (object)));
            var argumentArrayExpressions = Expression.NewArrayInit(typeof (object), argumentArrayElements);
            // The actual call to String.Format:
            var formatCallExpression = Expression.Call(formatMethod, patternExpression, argumentArrayExpressions);
            // The lambda expression we will be compiling:
            var lambdaExpression = Expression.Lambda<Func<T, string>>(formatCallExpression, parameterExpression);

            // The lambda expression will look something like this
            // input => string.Format("my format string", new[]{ input.Arg0, input.Arg1, ... });

            // Now we can compile the lambda expression
            var func = lambdaExpression.Compile();

            // Cache the pre-compiled expression 
            PrecompiledExpressions.TryAdd(pattern, func);

            // Execute the compiled expression
            return func(item);
        }

        private static IEnumerable<string> ParsePattern(string pattern, out string replacedPattern)
        {
            // Just replace each named format items with regular format items
            // and put all named format items in a list. Then return the 
            // new format string and the list of the named items.

            var sb = new StringBuilder();
            var lastIndex = 0;
            var arguments = new List<string>();
            var lowerarguments = new List<string>();

            foreach (var @group in from Match m in RegexFormatArgs.Matches(pattern)
                select m.Groups[m.Groups[6].Success ? 5 : 2])
            {
                var key = @group.Value;
                var lkey = key.ToLowerInvariant();
                var index = lowerarguments.IndexOf(lkey);
                if (index < 0)
                {
                    index = lowerarguments.Count;
                    lowerarguments.Add(lkey);
                    arguments.Add(key);
                }

                sb.Append(pattern.Substring(lastIndex, @group.Index - lastIndex));
                sb.Append(index);

                lastIndex = @group.Index + @group.Length;
            }

            sb.Append(pattern.Substring(lastIndex));
            replacedPattern = sb.ToString();
            return arguments;
        }
    }
}