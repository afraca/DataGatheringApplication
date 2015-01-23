using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
#if !NET_3_5
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

#endif

namespace CsvFiles
{
    public class CsvFile : IDisposable
    {
        protected static DateTime DateTimeZero = new DateTime();
        protected internal Stream BaseStream;
        public char FieldSeparator { get; private set; }
        public char TextQualifier { get; private set; }
        public IEnumerable<String> Columns { get; private set; }

        public void Dispose()
        {
            Dispose(true);
        }

        public static IEnumerable<T> Read<T>(CsvSource csvSource) where T : new()
        {
            var csvFileReader = new CsvFileReader<T>(csvSource);
            return csvFileReader;
        }

        protected virtual void Dispose(bool disposing)
        {
            // overriden in derived classes
        }

        #region Static Members

        public static CsvDefinition DefaultCsvDefinition { get; set; }
        public static bool UseLambdas { get; set; }
        public static bool UseTasks { get; set; }
        public static bool FastIndexOfAny { get; set; }

        static CsvFile()
        {
            // Choosing default Field Separator is a hard decision
            // In theory the C of CSV means Comma separated 
            // In Windows though many people will try to open the csv with Excel which is horrid with real CSV.
            // As the target for this library is Windows we go for Semi Colon.
            DefaultCsvDefinition = new CsvDefinition
            {
                EndOfLine = "\r\n",
                FieldSeparator = ';',
                TextQualifier = '"'
            };
            UseLambdas = true;
            UseTasks = true;
            FastIndexOfAny = true;
        }

        #endregion
    }

    public class CsvFile<T> : CsvFile
    {
        private readonly String[] columns;
        private readonly char fieldSeparator;
        private readonly string fieldSeparatorAsString;
        private readonly char[] invalidCharsInFields;
        private readonly bool[] isInvalidCharInFields;
        private readonly StreamWriter streamWriter;
        private readonly char textQualifier;
        private Func<T, object>[] getters;

        public CsvFile(CsvDestination csvDestination)
            : this(csvDestination, null)
        {
        }

        public CsvFile()
        {
        }

        public CsvFile(CsvDestination csvDestination, CsvDefinition csvDefinition)
        {
            if (csvDefinition == null)
                csvDefinition = DefaultCsvDefinition;
            columns = (csvDefinition.Columns ?? InferColumns(typeof (T))).ToArray();
            fieldSeparator = csvDefinition.FieldSeparator;
            fieldSeparatorAsString = fieldSeparator.ToString(CultureInfo.InvariantCulture);
            textQualifier = csvDefinition.TextQualifier;
            streamWriter = csvDestination.StreamWriter;

            invalidCharsInFields = new[] {'\r', '\n', textQualifier, fieldSeparator};
            isInvalidCharInFields = new bool[256];

            foreach (var c in invalidCharsInFields)
            {
                isInvalidCharInFields[c] = true;
            }
            WriteHeader();

            CreateGetters();
#if !NET_3_5
            if (UseTasks)
            {
                writeCsvLinesTask = new Thread(o => WriteCsvLines());
                writeCsvLinesTask.Start();
            }
            addAsyncTask = Task.Factory.StartNew(() => { });

#endif
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
#if !NET_3_5
                addAsyncTask.Wait();
                if (csvLinesToWrite != null)
                {
                    csvLinesToWrite.CompleteAdding();
                }
                if (writeCsvLinesTask != null)
                    writeCsvLinesTask.Join();
#endif
                streamWriter.Close();
            }
        }

        protected static IEnumerable<string> InferColumns(Type recordType)
        {
            var columns = recordType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(pi => pi.GetIndexParameters().Length == 0
                             && pi.GetSetMethod() != null
                             && !Attribute.IsDefined(pi, typeof (CsvIgnorePropertyAttribute)))
                .Select(pi => pi.Name)
                .Concat(recordType
                    .GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(fi => !Attribute.IsDefined(fi, typeof (CsvIgnorePropertyAttribute)))
                    .Select(fi => fi.Name))
                .ToList();
            return columns;
        }

#if !NET_3_5
        private void WriteCsvLines()
        {
            var written = 0;
            foreach (var csvLine in csvLinesToWrite.GetConsumingEnumerable())
            {
                streamWriter.WriteLine(csvLine);
                written++;
            }
            Interlocked.Add(ref linesToWrite, -written);
        }
#endif

        public void Append(T record)
        {
            if (UseTasks)
            {
#if !NET_3_5

                var linesWaiting = Interlocked.Increment(ref linesToWrite);
                Action<Task> addRecord = t =>
                {
                    var csvLine = ToCsv(record);
                    csvLinesToWrite.Add(csvLine);
                };

                if (linesWaiting < 10000)
                    addAsyncTask = addAsyncTask.ContinueWith(addRecord);
                else
                    addRecord(null);
#else
                throw new NotImplementedException("Tasks");
#endif
            }
            else
            {
                var csvLine = ToCsv(record);
                streamWriter.WriteLine(csvLine);
            }
        }

        private static Func<T, object> FindGetter(string c, bool staticMember)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase |
                        (staticMember ? BindingFlags.Static : BindingFlags.Instance);
            Func<T, object> func = null;
            var pi = typeof (T).GetProperty(c, flags);
            var fi = typeof (T).GetField(c, flags);

            if (UseLambdas)
            {
                Expression expr = null;
                var parameter = Expression.Parameter(typeof (T), "r");
                Type type = null;

                if (pi != null)
                {
                    type = pi.PropertyType;
                    expr = Expression.Property(parameter, pi.Name);
                }
                else if (fi != null)
                {
                    type = fi.FieldType;
                    expr = Expression.Field(parameter, fi.Name);
                }
                if (expr != null)
                {
                    Expression<Func<T, object>> lambda;
                    if (type.IsValueType)
                    {
                        lambda = Expression.Lambda<Func<T, object>>(Expression.TypeAs(expr, typeof (object)), parameter);
                    }
                    else
                    {
                        lambda = Expression.Lambda<Func<T, object>>(expr, parameter);
                    }
                    func = lambda.Compile();
                }
            }
            else
            {
                if (pi != null)
                    func = o => pi.GetValue(o, null);
                else if (fi != null)
                    func = o => fi.GetValue(o);
            }
            return func;
        }

        private void CreateGetters()
        {
            var list = new List<Func<T, object>>();

            foreach (var columnName in columns)
            {
                Func<T, Object> func = null;
                var propertyName = (columnName.IndexOf(' ') < 0 ? columnName : columnName.Replace(" ", ""));
                func = FindGetter(columnName, false) ?? FindGetter(columnName, true);

                list.Add(func);
            }
            getters = list.ToArray();
        }

        private string ToCsv(T record)
        {
            if (record == null)
                throw new ArgumentException("Cannot be null", "record");

            var csvStrings = new string[getters.Length];

            for (var i = 0; i < getters.Length; i++)
            {
                var getter = getters[i];
                var fieldValue = getter == null ? null : getter(record);
                csvStrings[i] = ToCsvString(fieldValue);
            }
            return string.Join(fieldSeparatorAsString, csvStrings);
        }

        private string ToCsvString(object o)
        {
            if (o != null)
            {
                var valueString = o as string ?? Convert.ToString(o, CultureInfo.CurrentUICulture);
                if (RequiresQuotes(valueString))
                {
                    var csvLine = new StringBuilder();
                    csvLine.Append(textQualifier);
                    foreach (var c in valueString)
                    {
                        if (c == textQualifier)
                            csvLine.Append(c); // double the double quotes
                        csvLine.Append(c);
                    }
                    csvLine.Append(textQualifier);
                    return csvLine.ToString();
                }
                return valueString;
            }
            return string.Empty;
        }

        private bool RequiresQuotes(string valueString)
        {
            if (FastIndexOfAny)
            {
                var len = valueString.Length;
                for (var i = 0; i < len; i++)
                {
                    var c = valueString[i];
                    if (c <= 255 && isInvalidCharInFields[c])
                        return true;
                }
                return false;
            }
            return valueString.IndexOfAny(invalidCharsInFields) >= 0;
        }

        private void WriteHeader()
        {
            var csvLine = new StringBuilder();
            for (var i = 0; i < columns.Length; i++)
            {
                if (i > 0)
                    csvLine.Append(fieldSeparator);
                csvLine.Append(ToCsvString(columns[i]));
            }
            streamWriter.WriteLine(csvLine.ToString());
        }

#if !NET_3_5
        private int linesToWrite;
        private readonly BlockingCollection<string> csvLinesToWrite = new BlockingCollection<string>(5000);
        private readonly Thread writeCsvLinesTask;
        private Task addAsyncTask;
#endif
    }

    internal class CsvFileReader<T> : CsvFile, IEnumerable<T>, IEnumerator<T>
        where T : new()
    {
        private readonly Dictionary<Type, List<Action<T, String>>> allSetters =
            new Dictionary<Type, List<Action<T, String>>>();

        private readonly char fieldSeparator;
        private readonly StringBuilder parseFieldResult = new StringBuilder();
        private readonly char textQualifier;
        private readonly TextReader textReader;
        private string[] columns;
        private char curChar;
        private int len;
        private string line;
        private int pos;

        public CsvFileReader(CsvSource csvSource)
            : this(csvSource, null)
        {
        }

        public CsvFileReader(CsvSource csvSource, CsvDefinition csvDefinition)
        {
            var streamReader = csvSource.TextReader as StreamReader;
            if (streamReader != null)
                BaseStream = streamReader.BaseStream;
            if (csvDefinition == null)
                csvDefinition = DefaultCsvDefinition;
            fieldSeparator = csvDefinition.FieldSeparator;
            textQualifier = csvDefinition.TextQualifier;

            textReader = csvSource.TextReader; // new FileStream(csvSource.TextReader, FileMode.Open);

            ReadHeader(csvDefinition.Header);
        }

        public bool Eof
        {
            get { return line == null; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this;
        }

        public T Current { get; private set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public bool MoveNext()
        {
            ReadNextLine();
            if (line == null && (line = textReader.ReadLine()) == null)
            {
                Current = default(T);
            }
            else
            {
                Current = new T();
                var recordType = typeof (T);
                List<Action<T, String>> setters;
                if (!allSetters.TryGetValue(recordType, out setters))
                {
                    setters = CreateSetters();
                    allSetters[recordType] = setters;
                }

                var fieldValues = new string[setters.Count];
                for (var i = 0; i < setters.Count; i++)
                {
                    fieldValues[i] = ParseField();
                    if (curChar == fieldSeparator)
                        NextChar();
                    else
                        break;
                }
                for (var i = 0; i < setters.Count; i++)
                {
                    var setter = setters[i];
                    if (setter != null)
                    {
                        setter(Current, fieldValues[i]);
                    }
                }
            }
            return (Current != null);
        }

        public void Reset()
        {
            throw new NotImplementedException("Cannot reset CsvFileReader enumeration.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                textReader.Dispose();
            }
        }

        private static Action<T, string> EmitSetValueAction(MemberInfo mi, Func<string, object> func)
        {
            var paramExpObj = Expression.Parameter(typeof (object), "obj");
            var paramExpT = Expression.Parameter(typeof (T), "instance");

            {
                var pi = mi as PropertyInfo;
                if (pi != null)
                {
#if !NET_3_5
                    if (UseLambdas)
                    {
                        var callExpr = Expression.Call(
                            paramExpT,
                            pi.GetSetMethod(),
                            Expression.ConvertChecked(paramExpObj, pi.PropertyType));
                        var setter = Expression.Lambda<Action<T, object>>(
                            callExpr,
                            paramExpT,
                            paramExpObj).Compile();
                        return (o, s) => setter(o, func(s));
                    }
#endif
                    return (o, v) => pi.SetValue(o, func(v), null);
                }
            }
            {
                var fi = mi as FieldInfo;
                if (fi != null)
                {
#if !NET_3_5
                    if (UseLambdas)
                    {
                        //ParameterExpression valueExp = Expression.Parameter(typeof(string), "value");
                        var valueExp = Expression.ConvertChecked(paramExpObj, fi.FieldType);

                        // Expression.Property can be used here as well
                        var fieldExp = Expression.Field(paramExpT, fi);
                        var assignExp = Expression.Assign(fieldExp, valueExp);

                        var setter = Expression.Lambda<Action<T, object>>
                            (assignExp, paramExpT, paramExpObj).Compile();

                        return (o, s) => setter(o, func(s));
                    }
#endif
                    return ((o, v) => fi.SetValue(o, func(v)));
                }
            }
            throw new NotImplementedException();
        }

        private static Action<T, string> FindSetter(string c, bool staticMember)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase |
                        (staticMember ? BindingFlags.Static : BindingFlags.Instance);
            Action<T, string> action = null;
            var pi = typeof (T).GetProperty(c, flags);
            if (pi != null)
            {
                var pFunc = StringToObject(pi.PropertyType);
                action = EmitSetValueAction(pi, pFunc);
            }
            var fi = typeof (T).GetField(c, flags);
            if (fi != null)
            {
                var fFunc = StringToObject(fi.FieldType);
                action = EmitSetValueAction(fi, fFunc);
            }
            return action;
        }

        private static Func<string, object> StringToObject(Type propertyType)
        {
            if (propertyType == typeof (string))
                return s => s ?? String.Empty;
            if (propertyType == typeof (Int32))
                return s => String.IsNullOrEmpty(s) ? 0 : Int32.Parse(s);
            if (propertyType == typeof (DateTime))
                return s => String.IsNullOrEmpty(s) ? DateTimeZero : DateTime.Parse(s);
            throw new NotImplementedException();
        }

        private List<Action<T, string>> CreateSetters()
        {
            var list = new List<Action<T, string>>();
            for (var i = 0; i < columns.Length; i++)
            {
                var columnName = columns[i];
                Action<T, string> action = null;
                if (columnName.IndexOf(' ') >= 0)
                    columnName = columnName.Replace(" ", "");
                action = FindSetter(columnName, false) ?? FindSetter(columnName, true);

                list.Add(action);
            }
            return list;
        }

        private void NextChar()
        {
            if (pos < len)
            {
                pos++;
                curChar = pos < len ? line[pos] : '\0';
            }
        }

        private void ParseEndOfLine()
        {
            throw new NotImplementedException();
        }

        private string ParseField()
        {
            parseFieldResult.Length = 0;
            if (line == null || pos >= len)
                return null;
            while (curChar == ' ' || curChar == '\t')
            {
                NextChar();
            }
            if (curChar == textQualifier)
            {
                NextChar();
                while (curChar != 0)
                {
                    if (curChar == textQualifier)
                    {
                        NextChar();
                        if (curChar == textQualifier)
                        {
                            NextChar();
                            parseFieldResult.Append(textQualifier);
                        }
                        else
                            return parseFieldResult.ToString();
                    }
                    else if (curChar == '\0')
                    {
                        if (line == null)
                            return parseFieldResult.ToString();
                        ReadNextLine();
                    }
                    else
                    {
                        parseFieldResult.Append(curChar);
                        NextChar();
                    }
                }
            }
            else
            {
                while (curChar != 0 && curChar != fieldSeparator && curChar != '\r' && curChar != '\n')
                {
                    parseFieldResult.Append(curChar);
                    NextChar();
                }
            }
            return parseFieldResult.ToString();
        }

        private void ReadHeader(string header)
        {
            if (header == null)
            {
                ReadNextLine();
            }
            else
            {
                // we read the first line from the given header
                line = header;
                pos = -1;
                len = line.Length;
                NextChar();
            }

            var readColumns = new List<string>();
            string columnName;
            while ((columnName = ParseField()) != null)
            {
                readColumns.Add(columnName);
                if (curChar == fieldSeparator)
                    NextChar();
                else
                    break;
            }
            columns = readColumns.ToArray();
        }

        private void ReadNextLine()
        {
            line = textReader.ReadLine();
            pos = -1;
            if (line == null)
            {
                len = 0;
                curChar = '\0';
            }
            else
            {
                len = line.Length;
                NextChar();
            }
        }
    }

    public class CsvDefinition
    {
        public CsvDefinition()
        {
            if (CsvFile.DefaultCsvDefinition != null)
            {
                FieldSeparator = CsvFile.DefaultCsvDefinition.FieldSeparator;
                TextQualifier = CsvFile.DefaultCsvDefinition.TextQualifier;
                EndOfLine = CsvFile.DefaultCsvDefinition.EndOfLine;
            }
        }

        public string Header { get; set; }
        public char FieldSeparator { get; set; }
        public char TextQualifier { get; set; }
        public IEnumerable<String> Columns { get; set; }
        public string EndOfLine { get; set; }
    }

    public class CsvSource
    {
        public readonly TextReader TextReader;

        public CsvSource(TextReader textReader)
        {
            TextReader = textReader;
        }

        public CsvSource(Stream stream)
        {
            TextReader = new StreamReader(stream);
        }

        public CsvSource(string path)
        {
            TextReader = new StreamReader(path);
        }

        public CsvSource(CsvFile csvFile)
        {
            TextReader = new StreamReader(csvFile.BaseStream);
        }

        public static implicit operator CsvSource(CsvFile csvFile)
        {
            return new CsvSource(csvFile);
        }

        public static implicit operator CsvSource(string path)
        {
            return new CsvSource(path);
        }

        public static implicit operator CsvSource(TextReader textReader)
        {
            return new CsvSource(textReader);
        }
    }

    public class CsvDestination
    {
        public StreamWriter StreamWriter;

        private CsvDestination(StreamWriter streamWriter)
        {
            StreamWriter = streamWriter;
        }

        private CsvDestination(Stream stream)
        {
            StreamWriter = new StreamWriter(stream);
        }

        public CsvDestination(string fullName)
        {
            FixCsvFileName(ref fullName);
            StreamWriter = new StreamWriter(fullName);
        }

        public static implicit operator CsvDestination(string path)
        {
            return new CsvDestination(path);
        }

        private static void FixCsvFileName(ref string fullName)
        {
            fullName = Path.GetFullPath(fullName);
            var path = Path.GetDirectoryName(fullName);
            if (path != null && !Directory.Exists(path))
                Directory.CreateDirectory(path);
            if (!String.Equals(Path.GetExtension(fullName), ".csv"))
                fullName += ".csv";
        }
    }

    public static class CsvFileLinqExtensions
    {
        public static void ToCsv<T>(this IEnumerable<T> source, CsvDestination csvDestination)
        {
            source.ToCsv(csvDestination, null);
        }

        public static void ToCsv<T>(this IEnumerable<T> source, CsvDestination csvDestination,
            CsvDefinition csvDefinition)
        {
            using (var csvFile = new CsvFile<T>(csvDestination, csvDefinition))
            {
                foreach (var record in source)
                {
                    csvFile.Append(record);
                }
            }
        }
    }

    public class CsvIgnorePropertyAttribute : Attribute
    {
        public override string ToString()
        {
            return "Ignore Property";
        }
    }

    // 2013-11-29 Version 1
    // 2014-01-06 Version 2: add CoryLuLu suggestions
}