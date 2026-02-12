#nullable enable

using System.Text;
using System.Text.Json;
using MonacoTypeEmitter.Model;

namespace MonacoTypeEmitter.Emitter;

/// <summary>
/// Emits C# source files from the intermediate Monaco type model.
/// Matches all existing patterns in MonacoEditorComponent/Monaco/.
/// </summary>
public sealed class CSharpEmitter
{
    private readonly MonacoModel _model;
    private readonly IgnoreList _ignoreList;
    private readonly string _outputRoot;
    private readonly HashSet<string> _knownEnumNames;

    /// <summary>
    /// Maps type names to their C# namespace for cross-namespace using directives.
    /// </summary>
    private readonly Dictionary<string, string> _typeToNamespace;

    /// <summary>
    /// Maps type names to their Monaco source namespace (e.g., "monaco.editor") for TypeDoc URL generation.
    /// </summary>
    private readonly Dictionary<string, string> _typeToSourceNamespace;

    /// <summary>
    /// Maps type names to their TypeDoc kind string ("interface", "enum", "class", "type") for URL patterns.
    /// </summary>
    private readonly Dictionary<string, string> _typeDocKinds;

    public CSharpEmitter(MonacoModel model, IgnoreList ignoreList, string outputRoot, string repoRoot)
    {
        _model = model;
        _ignoreList = ignoreList;
        _outputRoot = outputRoot;

        // Collect all known enum names for type resolution
        _knownEnumNames = [];
        // Build type-to-namespace map for cross-namespace resolution
        _typeToNamespace = new Dictionary<string, string>();
        _typeToSourceNamespace = new Dictionary<string, string>();
        _typeDocKinds = new Dictionary<string, string>();

        foreach (var ns in model.Namespaces)
        {
            var csharpNs = NameMapper.ToCSharpNamespace(ns.Name);

            foreach (var e in ns.Enums)
            {
                _knownEnumNames.Add(e.Name);
                _typeToNamespace.TryAdd(e.Name, csharpNs);
                _typeToSourceNamespace.TryAdd(e.Name, ns.Name);
                _typeDocKinds.TryAdd(e.Name, "enums");
            }
            foreach (var ta in ns.TypeAliases)
            {
                if (IsStringLiteralUnion(ta.Type))
                {
                    _knownEnumNames.Add(ta.Name);
                    _typeToNamespace.TryAdd(ta.Name, csharpNs);
                    _typeToSourceNamespace.TryAdd(ta.Name, ns.Name);
                    _typeDocKinds.TryAdd(ta.Name, "types");
                }
            }
            foreach (var iface in ns.Interfaces)
            {
                _typeToNamespace.TryAdd(iface.Name, csharpNs);
                _typeToSourceNamespace.TryAdd(iface.Name, ns.Name);
                _typeDocKinds.TryAdd(iface.Name, "interfaces");
            }
            foreach (var cls in ns.Classes)
            {
                _typeToNamespace.TryAdd(cls.Name, csharpNs);
                _typeToSourceNamespace.TryAdd(cls.Name, ns.Name);
                _typeDocKinds.TryAdd(cls.Name, "classes");
            }
        }
    }

    /// <summary>
    /// Emits all C# files. Returns the list of files written (repo-relative paths).
    /// </summary>
    public List<string> EmitAll()
    {
        var written = new List<string>();

        foreach (var ns in _model.Namespaces)
        {
            var csharpNs = NameMapper.ToCSharpNamespace(ns.Name);
            var relDir = NameMapper.ToRelativeDirectory(ns.Name);

            // Emit enums
            foreach (var e in ns.Enums)
            {
                var path = EmitEnum(e, csharpNs, relDir);
                if (path is not null)
                    written.Add(path);
            }

            // Emit type aliases that are string literal unions as enums
            foreach (var ta in ns.TypeAliases)
            {
                if (IsStringLiteralUnion(ta.Type))
                {
                    var path = EmitTypeAliasEnum(ta, csharpNs, relDir);
                    if (path is not null)
                        written.Add(path);
                }
            }

            // Emit interfaces
            foreach (var iface in ns.Interfaces)
            {
                var path = EmitInterface(iface, csharpNs, relDir);
                if (path is not null)
                    written.Add(path);
            }

            // Emit classes (concrete implementations that pair with interfaces)
            // Skip generic interfaces -- C# typeof() in attributes cannot use open type params.
            foreach (var iface in ns.Interfaces)
            {
                if (iface.Name.StartsWith("I") && iface.Name.Length > 1
                    && char.IsUpper(iface.Name[1]) && iface.TypeParameters.Count == 0)
                {
                    var className = iface.Name[1..];
                    var path = EmitConcreteClass(iface, className, csharpNs, relDir);
                    if (path is not null)
                        written.Add(path);
                }
            }

            // Emit actual classes from the model (like Uri, KeyMod)
            foreach (var cls in ns.Classes)
            {
                var path = EmitClass(cls, csharpNs, relDir);
                if (path is not null)
                    written.Add(path);
            }
        }

        return written;
    }

    private string? EmitEnum(EnumInfo enumInfo, string csharpNs, string relDir)
    {
        var fileName = $"{enumInfo.Name}.cs";
        var repoRelPath = GetRepoRelativePath(relDir, fileName);

        if (_ignoreList.IsIgnored(repoRelPath))
        {
            Console.Error.WriteLine($"  Skipping (ignored): {repoRelPath}");
            return null;
        }

        var sb = new StringBuilder();
        WriteAutoGeneratedHeader(sb);

        if (enumInfo.IsStringEnum)
        {
            sb.AppendLine("using System.Runtime.Serialization;");
            sb.AppendLine("using System.Text.Json.Serialization;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {csharpNs}");
        sb.AppendLine("{");

        WriteDocComment(sb, enumInfo.Documentation, "    ");
        WriteTypeDocRemarks(sb, enumInfo.Name, "    ");

        if (enumInfo.IsStringEnum)
        {
            sb.AppendLine($"    [JsonConverter(typeof(JsonStringEnumConverter<{enumInfo.Name}>))]");
        }

        sb.AppendLine($"    public enum {enumInfo.Name}");
        sb.AppendLine("    {");

        for (int i = 0; i < enumInfo.Members.Count; i++)
        {
            var member = enumInfo.Members[i];
            var memberName = NameMapper.ToCSharpEnumMemberName(member.Name);

            WriteDocComment(sb, member.Documentation, "        ");

            if (enumInfo.IsStringEnum)
            {
                var jsonValue = GetEnumMemberJsonValue(member);
                sb.AppendLine($"        [JsonStringEnumMemberName(\"{jsonValue}\")]");
                sb.AppendLine($"        [EnumMember(Value = \"{jsonValue}\")]");
            }

            if (!enumInfo.IsStringEnum && member.Value is not null)
            {
                var numValue = ResolveNumericValue(member.Value);
                if (numValue is not null)
                    sb.Append($"        {memberName} = {numValue}");
                else
                    sb.Append($"        {memberName}");
            }
            else
            {
                sb.Append($"        {memberName}");
            }

            if (i < enumInfo.Members.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile(relDir, fileName, sb.ToString());
        return repoRelPath;
    }

    private string? EmitTypeAliasEnum(TypeAliasInfo typeAlias, string csharpNs, string relDir)
    {
        var fileName = $"{typeAlias.Name}.cs";
        var repoRelPath = GetRepoRelativePath(relDir, fileName);

        if (_ignoreList.IsIgnored(repoRelPath))
        {
            Console.Error.WriteLine($"  Skipping (ignored): {repoRelPath}");
            return null;
        }

        var sb = new StringBuilder();
        WriteAutoGeneratedHeader(sb);
        sb.AppendLine("using System.Runtime.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {csharpNs}");
        sb.AppendLine("{");

        WriteDocComment(sb, typeAlias.Documentation, "    ");
        WriteTypeDocRemarks(sb, typeAlias.Name, "    ");

        sb.AppendLine($"    [JsonConverter(typeof(JsonStringEnumConverter<{typeAlias.Name}>))]");
        sb.AppendLine($"    public enum {typeAlias.Name}");
        sb.AppendLine("    {");

        var literals = GetStringLiteralValues(typeAlias.Type);
        for (int i = 0; i < literals.Count; i++)
        {
            var literal = literals[i];
            var memberName = NameMapper.ToCSharpEnumMemberName(literal);

            sb.AppendLine($"        [JsonStringEnumMemberName(\"{literal}\")]");
            sb.AppendLine($"        [EnumMember(Value = \"{literal}\")]");
            sb.Append($"        {memberName}");

            if (i < literals.Count - 1)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile(relDir, fileName, sb.ToString());
        return repoRelPath;
    }

    private string? EmitInterface(InterfaceInfo iface, string csharpNs, string relDir)
    {
        // Check if this interface has a matching concrete class
        // (I-prefix pattern: IFoo -> Foo)
        // Skip concrete pairing for generic interfaces -- C# cannot put
        // open type parameters in typeof() for converter attributes.
        bool hasConcrete = iface.Name.StartsWith("I")
            && iface.Name.Length > 1
            && char.IsUpper(iface.Name[1])
            && iface.TypeParameters.Count == 0;

        var fileName = $"{iface.Name}.cs";
        var repoRelPath = GetRepoRelativePath(relDir, fileName);

        if (_ignoreList.IsIgnored(repoRelPath))
        {
            Console.Error.WriteLine($"  Skipping (ignored): {repoRelPath}");
            return null;
        }

        var sb = new StringBuilder();
        WriteAutoGeneratedHeader(sb);

        // Collect using directives needed for cross-namespace types
        var usings = CollectUsings(csharpNs, iface.Properties, iface.Methods, iface.Extends,
            indexSignatures: iface.IndexSignatures, callSignatures: iface.CallSignatures);

        // Need System.Text.Json.Serialization for [JsonConverter] (concrete pairing) or [JsonPropertyName]
        bool needsJsonSerialization = hasConcrete || iface.Properties.Any(p =>
            NameMapper.NeedsJsonPropertyName(p.Name, NameMapper.ToCSharpPropertyName(p.Name)));
        if (needsJsonSerialization)
            usings.Add("System.Text.Json.Serialization");

        WriteUsings(sb, usings);

        sb.AppendLine();
        sb.AppendLine($"namespace {csharpNs}");
        sb.AppendLine("{");

        WriteDocComment(sb, iface.Documentation, "    ");
        WriteTypeDocRemarks(sb, iface.Name, "    ");

        if (hasConcrete)
        {
            var concreteName = iface.Name[1..];
            sb.AppendLine($"    [JsonConverter(typeof(Monaco.Helpers.InterfaceToClassConverter<{iface.Name}, {concreteName}>))]");
        }

        var extendsClause = "";
        if (iface.Extends.Count > 0)
        {
            var baseTypes = iface.Extends.Select(FormatTypeReference).ToList();
            extendsClause = $" : {string.Join(", ", baseTypes)}";
        }

        var declTypeParams = FormatTypeParameters(iface.TypeParameters);
        sb.AppendLine($"    public interface {iface.Name}{declTypeParams}{extendsClause}");
        sb.AppendLine("    {");

        foreach (var prop in iface.Properties)
        {
            WriteDocComment(sb, prop.Documentation, "        ");

            var csharpType = MapPropertyType(prop);
            var propName = NameMapper.ToCSharpPropertyName(prop.Name);

            if (NameMapper.NeedsJsonPropertyName(prop.Name, propName))
            {
                sb.AppendLine($"        [JsonPropertyName(\"{NameMapper.GetJsonWireName(prop.Name)}\")]");
            }

            var getter = prop.IsReadonly ? "get;" : "get; set;";
            sb.AppendLine($"        {csharpType} {propName} {{ {getter} }}");
            sb.AppendLine();
        }

        // Emit methods
        foreach (var method in iface.Methods)
        {
            WriteMethod(sb, method, "        ", isInterface: true);
        }

        // Emit index signatures as indexers
        foreach (var idx in iface.IndexSignatures)
        {
            WriteIndexSignature(sb, idx, "        ", isInterface: true);
        }

        // Emit call signatures
        foreach (var cs in iface.CallSignatures)
        {
            WriteCallSignature(sb, cs, "        ", isInterface: true);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile(relDir, fileName, sb.ToString());
        return repoRelPath;
    }

    private string? EmitConcreteClass(InterfaceInfo iface, string className, string csharpNs, string relDir)
    {
        var fileName = $"{className}.cs";
        var repoRelPath = GetRepoRelativePath(relDir, fileName);

        if (_ignoreList.IsIgnored(repoRelPath))
        {
            Console.Error.WriteLine($"  Skipping (ignored): {repoRelPath}");
            return null;
        }

        var sb = new StringBuilder();
        WriteAutoGeneratedHeader(sb);

        var usings = CollectUsings(csharpNs, iface.Properties, iface.Methods, iface.Extends,
            indexSignatures: iface.IndexSignatures, callSignatures: iface.CallSignatures);

        // Check if any property needs [JsonPropertyName] -- if so, import the serialization namespace
        bool needsJsonSerialization = iface.Properties.Any(p =>
            NameMapper.NeedsJsonPropertyName(p.Name, NameMapper.ToCSharpPropertyName(p.Name)));
        if (needsJsonSerialization)
            usings.Add("System.Text.Json.Serialization");

        WriteUsings(sb, usings);

        sb.AppendLine();
        sb.AppendLine($"namespace {csharpNs}");
        sb.AppendLine("{");

        WriteDocComment(sb, iface.Documentation, "    ");
        WriteTypeDocRemarks(sb, iface.Name, "    ");

        sb.AppendLine($"    public sealed class {className} : {iface.Name}");
        sb.AppendLine("    {");

        foreach (var prop in iface.Properties)
        {
            WriteDocComment(sb, prop.Documentation, "        ");

            var csharpType = MapPropertyType(prop);
            var propName = NameMapper.ToCSharpPropertyName(prop.Name);

            if (NameMapper.NeedsJsonPropertyName(prop.Name, propName))
            {
                sb.AppendLine($"        [JsonPropertyName(\"{NameMapper.GetJsonWireName(prop.Name)}\")]");
            }

            sb.AppendLine($"        public {csharpType} {propName} {{ get; set; }}");
            sb.AppendLine();
        }

        // Emit methods from interface (as public implementations)
        foreach (var method in iface.Methods)
        {
            WriteMethod(sb, method, "        ", isInterface: false);
        }

        // Emit index signatures as indexers
        foreach (var idx in iface.IndexSignatures)
        {
            WriteIndexSignature(sb, idx, "        ", isInterface: false);
        }

        // Emit call signatures
        foreach (var cs in iface.CallSignatures)
        {
            WriteCallSignature(sb, cs, "        ", isInterface: false);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile(relDir, fileName, sb.ToString());
        return repoRelPath;
    }

    private string? EmitClass(ClassInfo cls, string csharpNs, string relDir)
    {
        var fileName = $"{cls.Name}.cs";
        var repoRelPath = GetRepoRelativePath(relDir, fileName);

        if (_ignoreList.IsIgnored(repoRelPath))
        {
            Console.Error.WriteLine($"  Skipping (ignored): {repoRelPath}");
            return null;
        }

        var sb = new StringBuilder();
        WriteAutoGeneratedHeader(sb);

        // Build the list of base type references for using resolution
        var baseTypeRefs = new List<TypeReference>();
        if (cls.Extends is not null)
            baseTypeRefs.Add(cls.Extends);
        baseTypeRefs.AddRange(cls.Implements);

        var usings = CollectUsings(csharpNs, cls.Properties, cls.Methods, baseTypeRefs,
            indexSignatures: cls.IndexSignatures, constructors: cls.Constructors);

        // Check if any property needs [JsonPropertyName] -- if so, import the serialization namespace
        bool needsJsonSerialization = cls.Properties.Any(p =>
            NameMapper.NeedsJsonPropertyName(p.Name, NameMapper.ToCSharpPropertyName(p.Name)));
        if (needsJsonSerialization)
            usings.Add("System.Text.Json.Serialization");

        WriteUsings(sb, usings);

        sb.AppendLine();
        sb.AppendLine($"namespace {csharpNs}");
        sb.AppendLine("{");

        WriteDocComment(sb, cls.Documentation, "    ");
        WriteTypeDocRemarks(sb, cls.Name, "    ");

        var baseClause = "";
        var bases = new List<string>();
        if (cls.Extends is not null)
            bases.Add(FormatTypeReference(cls.Extends));
        foreach (var impl in cls.Implements)
            bases.Add(FormatTypeReference(impl));
        if (bases.Count > 0)
            baseClause = $" : {string.Join(", ", bases)}";

        var declTypeParams = FormatTypeParameters(cls.TypeParameters);
        sb.AppendLine($"    public sealed class {cls.Name}{declTypeParams}{baseClause}");
        sb.AppendLine("    {");

        foreach (var prop in cls.Properties)
        {
            WriteDocComment(sb, prop.Documentation, "        ");

            var csharpType = MapClassPropertyType(prop);
            var propName = NameMapper.ToCSharpPropertyName(prop.Name);

            if (NameMapper.NeedsJsonPropertyName(prop.Name, propName))
            {
                sb.AppendLine($"        [JsonPropertyName(\"{NameMapper.GetJsonWireName(prop.Name)}\")]");
            }

            var accessor = prop.IsReadonly ? "get;" : "get; set;";
            sb.AppendLine($"        public {csharpType} {propName} {{ {accessor} }}");
            sb.AppendLine();
        }

        // Emit constructors
        foreach (var ctor in cls.Constructors)
        {
            WriteConstructor(sb, ctor, cls.Name, "        ", cls.Properties);
        }

        // Emit methods
        foreach (var method in cls.Methods)
        {
            WriteMethod(sb, method, "        ", isInterface: false);
        }

        // Emit index signatures as indexers
        foreach (var idx in cls.IndexSignatures)
        {
            WriteIndexSignature(sb, idx, "        ", isInterface: false);
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        WriteFile(relDir, fileName, sb.ToString());
        return repoRelPath;
    }

    // --- Method emission ---

    private void WriteMethod(StringBuilder sb, MethodInfo method, string indent, bool isInterface)
    {
        WriteDocComment(sb, method.Documentation, indent);
        WriteParamDocs(sb, method.Parameters, indent);
        WriteReturnsDocs(sb, method.ReturnType, indent);

        var returnType = TypeMapper.ToCSharpType(method.ReturnType);
        var methodName = NameMapper.ToCSharpPropertyName(method.Name);
        var parameters = FormatParameters(method.Parameters);
        var typeParams = FormatTypeParameters(method.TypeParameters);

        if (isInterface)
        {
            sb.AppendLine($"{indent}{returnType} {methodName}{typeParams}({parameters});");
        }
        else
        {
            var staticMod = method.IsStatic ? "static " : "";
            sb.AppendLine($"{indent}public {staticMod}{returnType} {methodName}{typeParams}({parameters}) => throw new NotImplementedException();");
        }
        sb.AppendLine();

        // Emit overloads
        foreach (var overload in method.Overloads)
        {
            WriteDocComment(sb, overload.Documentation, indent);
            WriteParamDocs(sb, overload.Parameters, indent);
            WriteReturnsDocs(sb, overload.ReturnType, indent);

            var olReturnType = TypeMapper.ToCSharpType(overload.ReturnType);
            var olParams = FormatParameters(overload.Parameters);
            var olTypeParams = FormatTypeParameters(overload.TypeParameters);

            if (isInterface)
            {
                sb.AppendLine($"{indent}{olReturnType} {methodName}{olTypeParams}({olParams});");
            }
            else
            {
                var staticMod = method.IsStatic ? "static " : "";
                sb.AppendLine($"{indent}public {staticMod}{olReturnType} {methodName}{olTypeParams}({olParams}) => throw new NotImplementedException();");
            }
            sb.AppendLine();
        }
    }

    /// <summary>
    /// Emits a constructor for a class, with property assignments for matching parameters.
    /// </summary>
    private void WriteConstructor(StringBuilder sb, ConstructorInfo ctor, string className,
        string indent, List<PropertyInfo> classProperties)
    {
        WriteDocComment(sb, ctor.Documentation, indent);
        WriteParamDocs(sb, ctor.Parameters, indent);
        var parameters = FormatParameters(ctor.Parameters);

        // Match constructor params to class properties by name (case-insensitive)
        var propLookup = classProperties
            .ToDictionary(p => NameMapper.ToCSharpPropertyName(p.Name), p => p, StringComparer.OrdinalIgnoreCase);

        var assignments = new List<(string propName, string paramName)>();
        foreach (var param in ctor.Parameters)
        {
            var csharpParamName = EscapeCSharpKeyword(NameMapper.ToCSharpParameterName(param.Name));
            var propName = NameMapper.ToCSharpPropertyName(param.Name);
            if (propLookup.ContainsKey(propName))
            {
                assignments.Add((propName, csharpParamName));
            }
        }

        if (assignments.Count == 0)
        {
            sb.AppendLine($"{indent}public {className}({parameters}) {{ }}");
        }
        else
        {
            sb.AppendLine($"{indent}public {className}({parameters})");
            sb.AppendLine($"{indent}{{");
            foreach (var (propName, paramName) in assignments)
            {
                sb.AppendLine($"{indent}    {propName} = {paramName};");
            }
            sb.AppendLine($"{indent}}}");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Emits an index signature as a C# indexer.
    /// </summary>
    private void WriteIndexSignature(StringBuilder sb, IndexSignatureInfo idx, string indent, bool isInterface)
    {
        var keyType = TypeMapper.ToCSharpType(idx.KeyType);
        var valueType = TypeMapper.ToCSharpType(idx.ValueType);
        var accessor = idx.IsReadonly ? "get;" : "get; set;";

        if (isInterface)
        {
            sb.AppendLine($"{indent}{valueType} this[{keyType} {EscapeCSharpKeyword(NameMapper.ToCSharpParameterName(idx.KeyName))}] {{ {accessor} }}");
        }
        else
        {
            sb.AppendLine($"{indent}public {valueType} this[{keyType} {EscapeCSharpKeyword(NameMapper.ToCSharpParameterName(idx.KeyName))}] {{ {accessor} }}");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Emits a call signature as an Invoke method (C# does not support callable interfaces directly).
    /// </summary>
    private void WriteCallSignature(StringBuilder sb, CallSignatureInfo cs, string indent, bool isInterface)
    {
        WriteDocComment(sb, cs.Documentation, indent);
        WriteParamDocs(sb, cs.Parameters, indent);
        WriteReturnsDocs(sb, cs.ReturnType, indent);
        var returnType = TypeMapper.ToCSharpType(cs.ReturnType);
        var parameters = FormatParameters(cs.Parameters);
        var typeParams = FormatTypeParameters(cs.TypeParameters);

        if (isInterface)
        {
            sb.AppendLine($"{indent}{returnType} Invoke{typeParams}({parameters});");
        }
        else
        {
            sb.AppendLine($"{indent}public {returnType} Invoke{typeParams}({parameters}) => throw new NotImplementedException();");
        }
        sb.AppendLine();
    }

    private string FormatParameters(List<ParameterInfo> parameters)
    {
        if (parameters.Count == 0)
            return "";

        return string.Join(", ", parameters.Select(p =>
        {
            var type = TypeMapper.ToCSharpType(p.Type);
            var paramName = EscapeCSharpKeyword(NameMapper.ToCSharpParameterName(p.Name));

            if (p.IsRestParameter)
            {
                // TS rest param `...args: T[]` maps to `params T[] args`.
                // The mapped type is already T[], so use it directly.
                // If not already an array, wrap in [].
                // Do NOT apply optional/nullable for rest params (they accept zero elements).
                if (type.EndsWith("[]"))
                    return $"params {type} {paramName}";
                return $"params {type}[] {paramName}";
            }

            if (p.IsOptional && !type.EndsWith("?"))
            {
                if (TypeMapper.IsValueType(type) || TypeMapper.IsEnumType(type, _knownEnumNames))
                    type += "?";
                else
                    type += "?";
            }
            return $"{type} {paramName}";
        }));
    }

    private static string FormatTypeParameters(List<TypeParameterInfo> typeParams)
    {
        if (typeParams.Count == 0)
            return "";

        return $"<{string.Join(", ", typeParams.Select(tp => tp.Name))}>";
    }

    /// <summary>
    /// Formats a TypeReference (e.g., extends/implements) with its type arguments.
    /// For example: "IDisposable" or "IComparer&lt;T&gt;".
    /// </summary>
    private static string FormatTypeReference(TypeReference typeRef)
    {
        if (typeRef.TypeArguments.Count == 0)
            return typeRef.Name;

        var typeArgs = string.Join(", ", typeRef.TypeArguments.Select(TypeMapper.ToCSharpTypeArg));
        return $"{typeRef.Name}<{typeArgs}>";
    }

    private static string EscapeCSharpKeyword(string name)
    {
        // C# reserved keywords that might appear as parameter names
        var keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while", "value", "var"
        };

        return keywords.Contains(name) ? $"@{name}" : name;
    }

    // --- Using directive collection ---

    /// <summary>
    /// Collects using directives needed for cross-namespace type references,
    /// including base types from extends/implements clauses.
    /// </summary>
    private HashSet<string> CollectUsings(string currentNamespace,
        List<PropertyInfo> properties, List<MethodInfo> methods,
        IEnumerable<TypeReference>? baseTypeRefs = null,
        List<IndexSignatureInfo>? indexSignatures = null,
        List<CallSignatureInfo>? callSignatures = null,
        List<ConstructorInfo>? constructors = null)
    {
        var usings = new HashSet<string>();

        foreach (var prop in properties)
        {
            CollectTypeUsings(prop.Type, currentNamespace, usings);
        }

        foreach (var method in methods)
        {
            CollectTypeUsings(method.ReturnType, currentNamespace, usings);
            foreach (var param in method.Parameters)
            {
                CollectTypeUsings(param.Type, currentNamespace, usings);
            }
            foreach (var overload in method.Overloads)
            {
                CollectTypeUsings(overload.ReturnType, currentNamespace, usings);
                foreach (var param in overload.Parameters)
                {
                    CollectTypeUsings(param.Type, currentNamespace, usings);
                }
            }
        }

        // Resolve extends/implements base type references
        if (baseTypeRefs is not null)
        {
            foreach (var typeRef in baseTypeRefs)
            {
                if (_typeToNamespace.TryGetValue(typeRef.Name, out var ns) && ns != currentNamespace)
                {
                    usings.Add(ns);
                }
                foreach (var ta in typeRef.TypeArguments)
                {
                    CollectTypeUsings(ta, currentNamespace, usings);
                }
            }
        }

        // Resolve index signature types
        if (indexSignatures is not null)
        {
            foreach (var idx in indexSignatures)
            {
                CollectTypeUsings(idx.KeyType, currentNamespace, usings);
                CollectTypeUsings(idx.ValueType, currentNamespace, usings);
            }
        }

        // Resolve call signature types
        if (callSignatures is not null)
        {
            foreach (var cs in callSignatures)
            {
                CollectTypeUsings(cs.ReturnType, currentNamespace, usings);
                foreach (var param in cs.Parameters)
                {
                    CollectTypeUsings(param.Type, currentNamespace, usings);
                }
            }
        }

        // Resolve constructor parameter types
        if (constructors is not null)
        {
            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.Parameters)
                {
                    CollectTypeUsings(param.Type, currentNamespace, usings);
                }
            }
        }

        return usings;
    }

    private void CollectTypeUsings(TypeInfo type, string currentNamespace, HashSet<string> usings)
    {
        switch (type.Kind)
        {
            case "reference":
                if (type.Name is not null && _typeToNamespace.TryGetValue(type.Name, out var ns))
                {
                    if (ns != currentNamespace)
                        usings.Add(ns);
                }
                if (type.TypeArguments is not null)
                {
                    foreach (var ta in type.TypeArguments)
                        CollectTypeUsings(ta, currentNamespace, usings);
                }
                break;
            case "array":
                if (type.ElementType is not null)
                    CollectTypeUsings(type.ElementType, currentNamespace, usings);
                break;
            case "union":
            case "intersection":
                if (type.Types is not null)
                {
                    foreach (var t in type.Types)
                        CollectTypeUsings(t, currentNamespace, usings);
                }
                break;
            case "typeOperator":
                if (type.Type is not null)
                    CollectTypeUsings(type.Type, currentNamespace, usings);
                break;
        }
    }

    private static void WriteUsings(StringBuilder sb, HashSet<string> usings)
    {
        if (usings.Count == 0)
            return;

        foreach (var u in usings.OrderBy(x => x))
        {
            sb.AppendLine($"using {u};");
        }
    }

    // --- Property type mapping ---

    private string MapPropertyType(PropertyInfo prop)
    {
        var baseType = TypeMapper.ToCSharpType(prop.Type);

        // If optional, make nullable
        if (prop.IsOptional)
        {
            if (TypeMapper.IsValueType(baseType) || TypeMapper.IsEnumType(baseType, _knownEnumNames))
            {
                return baseType + "?";
            }

            // Reference types (including arrays): add ? suffix if not already nullable
            if (!baseType.EndsWith("?"))
                return baseType + "?";
        }

        return baseType;
    }

    private string MapClassPropertyType(PropertyInfo prop)
    {
        var baseType = TypeMapper.ToCSharpType(prop.Type);

        // For class properties, optional means nullable
        if (prop.IsOptional)
        {
            if (TypeMapper.IsValueType(baseType) || TypeMapper.IsEnumType(baseType, _knownEnumNames))
            {
                return baseType + "?";
            }

            // Reference types (including arrays): add ? suffix if not already nullable
            if (!baseType.EndsWith("?"))
                return baseType + "?";
        }

        return baseType;
    }

    // --- Enum helpers ---

    private static string GetEnumMemberJsonValue(EnumMemberInfo member)
    {
        // If the enum member has an explicit string value, use it
        if (member.Value is string strVal)
            return strVal;

        // Handle JsonElement (STJ deserializes object? as JsonElement)
        if (member.Value is JsonElement je && je.ValueKind == JsonValueKind.String)
            return je.GetString() ?? NameMapper.ToCamelCase(member.Name);

        // Otherwise, use the camelCase form of the member name
        return NameMapper.ToCamelCase(member.Name);
    }

    /// <summary>
    /// Extracts a numeric value from an object that may be a JsonElement.
    /// </summary>
    private static long? ResolveNumericValue(object? value)
    {
        if (value is null)
            return null;

        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.Number => je.TryGetInt64(out var l) ? l : (long)je.GetDouble(),
                _ => null
            };
        }

        if (value is IConvertible conv)
            return conv.ToInt64(null);

        return null;
    }

    /// <summary>
    /// Resolves a string value from an object that may be a JsonElement.
    /// Only returns a value for actual strings -- does not use ToString() fallback
    /// to prevent numeric/bool literals from being misclassified as string values.
    /// </summary>
    private static string? ResolveStringValue(object? value)
    {
        if (value is null)
            return null;

        if (value is string s)
            return s;

        if (value is JsonElement je && je.ValueKind == JsonValueKind.String)
            return je.GetString();

        // Do NOT fall back to ToString() -- non-string values (numbers, bools)
        // must not be treated as string literal union members.
        return null;
    }

    private static bool IsStringLiteralUnion(TypeInfo type)
    {
        if (type.Kind != "union" || type.Types is null)
            return false;

        var nonNull = type.Types
            .Where(t => !(t.Kind == "primitive" && (t.Name == "null" || t.Name == "undefined")))
            .ToList();

        // Must be all string literals -- ResolveStringValue returns null for non-strings
        return nonNull.Count > 0 && nonNull.All(t =>
            t.Kind == "literal" && ResolveStringValue(t.Value) is not null);
    }

    private static List<string> GetStringLiteralValues(TypeInfo type)
    {
        if (type.Kind != "union" || type.Types is null)
            return [];

        return type.Types
            .Where(t => t.Kind == "literal" && ResolveStringValue(t.Value) is not null)
            .Select(t => ResolveStringValue(t.Value)!)
            .ToList();
    }

    // --- File output helpers ---

    private static string GetRepoRelativePath(string relDir, string fileName)
    {
        if (string.IsNullOrEmpty(relDir))
            return $"MonacoEditorComponent/Monaco/{fileName}";

        return $"MonacoEditorComponent/Monaco/{relDir}/{fileName}";
    }

    private void WriteFile(string relDir, string fileName, string content)
    {
        var dir = string.IsNullOrEmpty(relDir)
            ? _outputRoot
            : Path.Combine(_outputRoot, relDir);

        Directory.CreateDirectory(dir);

        var fullPath = Path.Combine(dir, fileName);
        File.WriteAllText(fullPath, content);
        Console.Error.WriteLine($"  Written: {GetRepoRelativePath(relDir, fileName)}");
    }

    private static void WriteAutoGeneratedHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
    }

    private static void WriteDocComment(StringBuilder sb, string? documentation, string indent)
    {
        if (string.IsNullOrWhiteSpace(documentation))
            return;

        sb.AppendLine($"{indent}/// <summary>");
        foreach (var line in documentation.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(trimmed))
                sb.AppendLine($"{indent}///");
            else
                sb.AppendLine($"{indent}/// {EscapeXml(trimmed)}");
        }
        sb.AppendLine($"{indent}/// </summary>");
    }

    /// <summary>
    /// Writes <c>&lt;param&gt;</c> XML doc tags for parameters that have documentation.
    /// </summary>
    private static void WriteParamDocs(StringBuilder sb, List<ParameterInfo> parameters, string indent)
    {
        foreach (var param in parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Documentation))
                continue;

            var paramName = EscapeCSharpKeyword(NameMapper.ToCSharpParameterName(param.Name));
            // Remove @ prefix for XML doc param name (C# uses the unescaped name in docs)
            if (paramName.StartsWith("@"))
                paramName = paramName[1..];

            // Collapse multi-line documentation into a single line for <param> tags
            var doc = CollapseToSingleLine(param.Documentation);
            sb.AppendLine($"{indent}/// <param name=\"{paramName}\">{EscapeXml(doc)}</param>");
        }
    }

    /// <summary>
    /// Writes a <c>&lt;returns&gt;</c> XML doc tag for methods with non-void return types.
    /// Only emits the tag when there is meaningful return type context.
    /// For TypeScript type predicates, notes the original TS semantics.
    /// </summary>
    private static void WriteReturnsDocs(StringBuilder sb, TypeInfo returnType, string indent)
    {
        // Do not emit <returns> for void or primitive void returns
        if (returnType.Kind == "primitive" && returnType.Name is "void" or "undefined")
            return;

        // TypeScript type predicates get a specific return doc noting the TS semantics
        if (returnType.Kind == "intrinsic" && returnType.Text is not null
            && TypeMapper.IsTypePredicatePattern(returnType.Text))
        {
            sb.AppendLine($"{indent}/// <returns>True if the argument satisfies the TypeScript type predicate <c>{EscapeXml(returnType.Text)}</c>.</returns>");
            return;
        }

        // Emit a brief returns tag based on the return type
        var returnDesc = FormatReturnTypeDescription(returnType);
        if (returnDesc is not null)
        {
            sb.AppendLine($"{indent}/// <returns>{EscapeXml(returnDesc)}</returns>");
        }
    }

    /// <summary>
    /// Formats a human-readable description of a return type for use in <c>&lt;returns&gt;</c> tags.
    /// </summary>
    private static string? FormatReturnTypeDescription(TypeInfo returnType)
    {
        return returnType.Kind switch
        {
            "primitive" when returnType.Name is "string" => "A string value.",
            "primitive" when returnType.Name is "number" => "A numeric value.",
            "primitive" when returnType.Name is "boolean" => "A boolean value.",
            "reference" when returnType.Name is "Promise" or "PromiseLike" or "Thenable"
                => $"A task representing the asynchronous operation.",
            "reference" => $"A {returnType.Name} instance.",
            "array" => "An array of results.",
            _ => null
        };
    }

    /// <summary>
    /// Writes a <c>&lt;remarks&gt;</c> block with a <c>&lt;see href="..."/&gt;</c> link
    /// to the corresponding Monaco TypeDoc API page for a type.
    /// </summary>
    private void WriteTypeDocRemarks(StringBuilder sb, string typeName, string indent)
    {
        var url = GetTypeDocUrl(typeName);
        if (url is null)
            return;

        sb.AppendLine($"{indent}/// <remarks>");
        sb.AppendLine($"{indent}/// See <see href=\"{url}\">Monaco API</see> for more details.");
        sb.AppendLine($"{indent}/// </remarks>");
    }

    /// <summary>
    /// Constructs the Monaco TypeDoc URL for the given type name based on its source namespace and kind.
    /// </summary>
    /// <remarks>
    /// TODO: The generated URLs use the older <c>editor.{TypeName}</c> module path pattern which now
    /// returns 404. Monaco's TypeDoc site was regenerated with the <c>editor_editor_api.editor.{TypeName}</c>
    /// pattern. The namespace prefix logic needs updating to prepend <c>editor_editor_api.</c> to produce
    /// working URLs (e.g., <c>interfaces/editor_editor_api.editor.IMarkerData.html</c>).
    /// </remarks>
    private string? GetTypeDocUrl(string typeName)
    {
        if (!_typeToSourceNamespace.TryGetValue(typeName, out var sourceNs))
            return null;

        if (!_typeDocKinds.TryGetValue(typeName, out var kindPath))
            return null;

        const string baseUrl = "https://microsoft.github.io/monaco-editor/typedoc";

        // Build the namespace prefix for the URL
        // "monaco" -> no prefix, "monaco.editor" -> "editor.", "monaco.languages" -> "languages."
        var nsPrefix = GetTypeDocNamespacePrefix(sourceNs);

        return $"{baseUrl}/{kindPath}/{nsPrefix}{typeName}.html";
    }

    /// <summary>
    /// Extracts the TypeDoc namespace prefix from a Monaco source namespace.
    /// "monaco" -> "", "monaco.editor" -> "editor.", "monaco.languages" -> "languages."
    /// </summary>
    private static string GetTypeDocNamespacePrefix(string sourceNamespace)
    {
        var parts = sourceNamespace.Split('.');
        if (parts.Length <= 1)
            return "";

        return string.Join(".", parts.Skip(1)) + ".";
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    /// <summary>
    /// Collapses multi-line text into a single line by replacing newlines with spaces.
    /// </summary>
    private static string CollapseToSingleLine(string text)
    {
        var collapsed = text
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ')
            .Trim();

        // Collapse multiple consecutive spaces into one
        while (collapsed.Contains("  "))
            collapsed = collapsed.Replace("  ", " ");

        return collapsed;
    }
}
