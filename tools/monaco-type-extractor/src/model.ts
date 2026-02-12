/**
 * Intermediate JSON model for Monaco type information.
 *
 * This schema is the contract between the ts-morph type extractor (Node.js)
 * and the .NET CLI emitter. It must be versioned for forward compatibility.
 *
 * All output arrays are sorted alphabetically by `name` to ensure
 * deterministic ordering and prevent snapshot churn.
 */

/** Root of the intermediate model. */
export interface MonacoModel {
  /** Schema version for forward compatibility. Bump on breaking changes. */
  schemaVersion: number;
  /** Source file path that was parsed. */
  sourceFile: string;
  /** ISO 8601 timestamp of extraction. */
  extractedAt: string;
  /** Top-level namespaces (e.g., "monaco", "monaco.editor", "monaco.languages"). */
  namespaces: NamespaceInfo[];
}

/** A namespace containing types. */
export interface NamespaceInfo {
  /** Fully qualified namespace name (e.g., "monaco.editor"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Interfaces declared in this namespace. */
  interfaces: InterfaceInfo[];
  /** Enums declared in this namespace. */
  enums: EnumInfo[];
  /** Type aliases declared in this namespace. */
  typeAliases: TypeAliasInfo[];
  /** Classes declared in this namespace. */
  classes: ClassInfo[];
  /** Standalone functions declared in this namespace. */
  functions: FunctionInfo[];
}

/** Information about an interface declaration. */
export interface InterfaceInfo {
  /** Interface name (e.g., "IEditorOptions"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters (e.g., ["T", "U"]). */
  typeParameters: TypeParameterInfo[];
  /** Base types this interface extends. */
  extends: TypeReference[];
  /** Properties declared on this interface. */
  properties: PropertyInfo[];
  /** Methods declared on this interface. */
  methods: MethodInfo[];
  /** Index signatures (e.g., [key: string]: value). */
  indexSignatures: IndexSignatureInfo[];
  /** Call signatures (callable interfaces). */
  callSignatures: CallSignatureInfo[];
}

/** Information about a class declaration. */
export interface ClassInfo {
  /** Class name (e.g., "Uri"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** Base class, if any. */
  extends?: TypeReference;
  /** Interfaces this class implements. */
  implements: TypeReference[];
  /** Properties declared on this class. */
  properties: PropertyInfo[];
  /** Methods declared on this class. */
  methods: MethodInfo[];
  /** Constructor signatures. */
  constructors: ConstructorInfo[];
  /** Index signatures. */
  indexSignatures: IndexSignatureInfo[];
}

/** Information about an enum declaration. */
export interface EnumInfo {
  /** Enum name (e.g., "KeyCode"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /**
   * Whether this enum is string-backed or numeric.
   * Critical for the .NET emitter: string-backed enums need JsonStringEnumConverter,
   * numeric enums do not.
   */
  isStringEnum: boolean;
  /** Enum members. */
  members: EnumMemberInfo[];
}

/** Information about a single enum member. */
export interface EnumMemberInfo {
  /** Member name (e.g., "Unnecessary"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** The value, either a string or number. Undefined if auto-assigned. */
  value?: string | number;
}

/** Information about a type alias declaration. */
export interface TypeAliasInfo {
  /** Type alias name (e.g., "BuiltinTheme"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** The underlying type this alias resolves to. */
  type: TypeInfo;
}

/** Information about a property. */
export interface PropertyInfo {
  /** Property name (e.g., "lineNumber"). */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** The property's type. */
  type: TypeInfo;
  /** Whether the property is optional (?). */
  isOptional: boolean;
  /** Whether the property is readonly. */
  isReadonly: boolean;
}

/** Information about a method. */
export interface MethodInfo {
  /** Method name. */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** Method parameters. */
  parameters: ParameterInfo[];
  /** Return type. */
  returnType: TypeInfo;
  /** Method overloads (additional signatures beyond the primary). */
  overloads: MethodOverloadInfo[];
  /** Whether this is a static method. */
  isStatic: boolean;
}

/** A method overload (additional signature). */
export interface MethodOverloadInfo {
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** Overload parameters. */
  parameters: ParameterInfo[];
  /** Overload return type. */
  returnType: TypeInfo;
}

/** Information about a constructor. */
export interface ConstructorInfo {
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Constructor parameters. */
  parameters: ParameterInfo[];
}

/** Information about a function parameter. */
export interface ParameterInfo {
  /** Parameter name. */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** The parameter's type. */
  type: TypeInfo;
  /** Whether the parameter is optional. */
  isOptional: boolean;
  /** Whether the parameter is a rest parameter (...args). */
  isRestParameter: boolean;
}

/** Information about a generic type parameter. */
export interface TypeParameterInfo {
  /** Type parameter name (e.g., "T"). */
  name: string;
  /** Constraint type, if any (e.g., "extends object"). */
  constraint?: TypeInfo;
  /** Default type, if any. */
  default?: TypeInfo;
}

/** Information about a standalone function. */
export interface FunctionInfo {
  /** Function name. */
  name: string;
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** Function parameters. */
  parameters: ParameterInfo[];
  /** Return type. */
  returnType: TypeInfo;
  /** Function overloads. */
  overloads: MethodOverloadInfo[];
}

/** An index signature (e.g., [key: string]: SomeType). */
export interface IndexSignatureInfo {
  /** The key parameter name (usually "key" or "index"). */
  keyName: string;
  /** The key type (usually "string" or "number"). */
  keyType: TypeInfo;
  /** The value type. */
  valueType: TypeInfo;
  /** Whether the index signature is readonly. */
  isReadonly: boolean;
}

/** A call signature for callable interfaces. */
export interface CallSignatureInfo {
  /** JSDoc comment, if any. */
  documentation?: string;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
  /** Call signature parameters. */
  parameters: ParameterInfo[];
  /** Return type. */
  returnType: TypeInfo;
}

/** A reference to a type (used in extends/implements). */
export interface TypeReference {
  /** The type name or expression text. */
  name: string;
  /** Generic type arguments, if any. */
  typeArguments: TypeInfo[];
}

// ---- Structured type representation ----

/**
 * Discriminated union for type information.
 * Each variant has a `kind` field for dispatching.
 */
export type TypeInfo =
  | PrimitiveType
  | ReferenceType
  | UnionType
  | IntersectionType
  | ArrayType
  | TupleType
  | LiteralType
  | FunctionType
  | ObjectLiteralType
  | IndexedAccessType
  | TypeOperatorType
  | ConditionalType
  | IntrinsicType;

export interface PrimitiveType {
  kind: "primitive";
  /** e.g., "string", "number", "boolean", "void", "null", "undefined", "any", "unknown", "never", "bigint", "symbol", "object" */
  name: string;
}

export interface ReferenceType {
  kind: "reference";
  /** The referenced type name. */
  name: string;
  /** Generic type arguments. */
  typeArguments: TypeInfo[];
}

export interface UnionType {
  kind: "union";
  /** Constituent types in the union. */
  types: TypeInfo[];
}

export interface IntersectionType {
  kind: "intersection";
  /** Constituent types in the intersection. */
  types: TypeInfo[];
}

export interface ArrayType {
  kind: "array";
  /** The element type. */
  elementType: TypeInfo;
}

export interface TupleType {
  kind: "tuple";
  /** Element types in the tuple. */
  elementTypes: TypeInfo[];
}

export interface LiteralType {
  kind: "literal";
  /** The literal value. */
  value: string | number | boolean;
}

export interface FunctionType {
  kind: "function";
  /** Function type parameters. */
  parameters: ParameterInfo[];
  /** Function return type. */
  returnType: TypeInfo;
  /** Generic type parameters. */
  typeParameters: TypeParameterInfo[];
}

export interface ObjectLiteralType {
  kind: "objectLiteral";
  /** Properties in the object literal type. */
  properties: PropertyInfo[];
  /** Index signatures in the object literal type. */
  indexSignatures: IndexSignatureInfo[];
  /** Call signatures in the object literal type. */
  callSignatures: CallSignatureInfo[];
}

export interface IndexedAccessType {
  kind: "indexedAccess";
  /** The object type being indexed. */
  objectType: TypeInfo;
  /** The index type. */
  indexType: TypeInfo;
}

export interface TypeOperatorType {
  kind: "typeOperator";
  /** The operator (e.g., "keyof", "typeof", "readonly"). */
  operator: string;
  /** The target type. */
  type: TypeInfo;
}

export interface ConditionalType {
  kind: "conditional";
  /** Raw text representation (conditional types are complex). */
  text: string;
}

export interface IntrinsicType {
  kind: "intrinsic";
  /** Raw text for types that cannot be decomposed further. */
  text: string;
}
