/**
 * Core extractor that parses monaco.d.ts via ts-morph and produces
 * the intermediate JSON model defined in model.ts.
 */
import {
  Project,
  SourceFile,
  ModuleDeclaration,
  InterfaceDeclaration,
  EnumDeclaration,
  TypeAliasDeclaration,
  ClassDeclaration,
  FunctionDeclaration,
  TypeNode,
  Node,
  SyntaxKind,
  PropertySignature,
  MethodSignature,
  ParameterDeclaration,
  TypeParameterDeclaration,
  IndexSignatureDeclaration,
  CallSignatureDeclaration,
  PropertyDeclaration,
  MethodDeclaration,
  ConstructorDeclaration,
  GetAccessorDeclaration,
  TypeOperatorTypeNode,
  ts,
} from "ts-morph";

import type {
  MonacoModel,
  NamespaceInfo,
  InterfaceInfo,
  EnumInfo,
  TypeAliasInfo,
  ClassInfo,
  FunctionInfo,
  PropertyInfo,
  MethodInfo,
  MethodOverloadInfo,
  ConstructorInfo,
  ParameterInfo,
  TypeParameterInfo,
  TypeInfo,
  TypeReference,
  IndexSignatureInfo,
  CallSignatureInfo,
  EnumMemberInfo,
} from "./model.js";

/** Current schema version. Bump when making breaking changes to the model. */
const SCHEMA_VERSION = 1;

/**
 * Extract the Monaco type model from a .d.ts file.
 */
export function extractMonacoModel(filePath: string): MonacoModel {
  const project = new Project({
    compilerOptions: {
      target: ts.ScriptTarget.ES2020,
      strict: true,
    },
    skipAddingFilesFromTsConfig: true,
  });

  const sourceFile = project.addSourceFileAtPath(filePath);
  const namespaces = extractNamespaces(sourceFile);

  return {
    schemaVersion: SCHEMA_VERSION,
    sourceFile: filePath,
    extractedAt: new Date().toISOString(),
    namespaces: namespaces.sort(sortByName),
  };
}

// ---- Namespace extraction ----

function extractNamespaces(sourceFile: SourceFile): NamespaceInfo[] {
  const result: NamespaceInfo[] = [];
  const moduleDeclarations = sourceFile.getModules();

  for (const mod of moduleDeclarations) {
    collectNamespaces(mod, "", result);
  }

  return result;
}

function collectNamespaces(
  mod: ModuleDeclaration,
  parentPrefix: string,
  result: NamespaceInfo[]
): void {
  const name = mod.getName().replace(/['"]/g, "");
  const fullName = parentPrefix ? `${parentPrefix}.${name}` : name;

  const nsInfo: NamespaceInfo = {
    name: fullName,
    documentation: getDocumentation(mod),
    interfaces: extractInterfaces(mod).sort(sortByName),
    enums: extractEnums(mod).sort(sortByName),
    typeAliases: extractTypeAliases(mod).sort(sortByName),
    classes: extractClasses(mod).sort(sortByName),
    functions: extractFunctions(mod).sort(sortByName),
  };

  // Only include namespaces that have direct type declarations
  if (
    nsInfo.interfaces.length > 0 ||
    nsInfo.enums.length > 0 ||
    nsInfo.typeAliases.length > 0 ||
    nsInfo.classes.length > 0 ||
    nsInfo.functions.length > 0
  ) {
    result.push(nsInfo);
  }

  // Recursively handle nested namespaces
  for (const nested of mod.getModules()) {
    collectNamespaces(nested, fullName, result);
  }
}

// ---- Interface extraction ----

function extractInterfaces(mod: ModuleDeclaration): InterfaceInfo[] {
  return mod.getInterfaces().map((iface) => extractInterface(iface));
}

function extractInterface(iface: InterfaceDeclaration): InterfaceInfo {
  return {
    name: iface.getName(),
    documentation: getDocumentation(iface),
    typeParameters: extractTypeParameters(iface.getTypeParameters()),
    extends: iface.getExtends().map(extractTypeReferenceFromExpression),
    properties: extractPropertySignatures(iface.getProperties()).sort(
      sortByName
    ),
    methods: extractMethodSignatures(iface.getMethods()).sort(sortByName),
    indexSignatures: iface
      .getIndexSignatures()
      .map(extractIndexSignature)
      .sort(sortByKeyName),
    callSignatures: iface
      .getCallSignatures()
      .map(extractCallSignature),
  };
}

// ---- Enum extraction ----

function extractEnums(mod: ModuleDeclaration): EnumInfo[] {
  return mod.getEnums().map((e) => extractEnum(e));
}

function extractEnum(enumDecl: EnumDeclaration): EnumInfo {
  const members = enumDecl.getMembers().map((member): EnumMemberInfo => {
    const value = member.getValue();
    return {
      name: member.getName(),
      documentation: getDocumentation(member),
      value: value,
    };
  });

  // Determine if string-backed: if any member has an explicitly string value,
  // or if this is a const enum with string initializers
  const isStringEnum = members.some(
    (m) => typeof m.value === "string"
  );

  return {
    name: enumDecl.getName(),
    documentation: getDocumentation(enumDecl),
    isStringEnum,
    members: members.sort(sortByName),
  };
}

// ---- Type alias extraction ----

function extractTypeAliases(mod: ModuleDeclaration): TypeAliasInfo[] {
  return mod.getTypeAliases().map((ta) => extractTypeAlias(ta));
}

function extractTypeAlias(ta: TypeAliasDeclaration): TypeAliasInfo {
  const typeNode = ta.getTypeNode();
  return {
    name: ta.getName(),
    documentation: getDocumentation(ta),
    typeParameters: extractTypeParameters(ta.getTypeParameters()),
    type: typeNode
      ? extractTypeFromNode(typeNode)
      : { kind: "intrinsic", text: ta.getType().getText() },
  };
}

// ---- Class extraction ----

function extractClasses(mod: ModuleDeclaration): ClassInfo[] {
  return mod.getClasses().map((cls) => extractClass(cls));
}

function extractClass(cls: ClassDeclaration): ClassInfo {
  const extendsExpr = cls.getExtends();
  return {
    name: cls.getName() ?? "(anonymous)",
    documentation: getDocumentation(cls),
    typeParameters: extractTypeParameters(cls.getTypeParameters()),
    extends: extendsExpr
      ? extractTypeReferenceFromExpression(extendsExpr)
      : undefined,
    implements: cls
      .getImplements()
      .map(extractTypeReferenceFromExpression),
    properties: [
      ...extractClassProperties(cls.getProperties()),
      ...extractGetAccessors(cls.getGetAccessors()),
    ].sort(sortByName),
    methods: extractClassMethods(cls.getMethods()).sort(sortByName),
    constructors: cls.getConstructors().map(extractConstructor),
    // Classes in ts-morph don't expose getIndexSignatures; Monaco classes don't use them
    indexSignatures: [],
  };
}

function extractConstructor(ctor: ConstructorDeclaration): ConstructorInfo {
  return {
    documentation: getDocumentation(ctor),
    parameters: ctor.getParameters().map(extractParameter),
  };
}

// ---- Function extraction ----

function extractFunctions(mod: ModuleDeclaration): FunctionInfo[] {
  const functionMap = new Map<string, FunctionDeclaration[]>();

  // Group overloads by name
  for (const fn of mod.getFunctions()) {
    const name = fn.getName() ?? "(anonymous)";
    const existing = functionMap.get(name);
    if (existing) {
      existing.push(fn);
    } else {
      functionMap.set(name, [fn]);
    }
  }

  const result: FunctionInfo[] = [];

  for (const [name, overloads] of functionMap) {
    // The implementation signature is the last one; overloads are the preceding ones
    // In .d.ts files, there is no implementation - all are declaration signatures
    const primary = overloads[0];
    const rest = overloads.slice(1);

    result.push({
      name,
      documentation: getDocumentation(primary),
      typeParameters: extractTypeParameters(primary.getTypeParameters()),
      parameters: primary.getParameters().map(extractParameter),
      returnType: extractReturnType(primary.getReturnTypeNode()),
      overloads: rest.map(
        (fn): MethodOverloadInfo => ({
          documentation: getDocumentation(fn),
          typeParameters: extractTypeParameters(fn.getTypeParameters()),
          parameters: fn.getParameters().map(extractParameter),
          returnType: extractReturnType(fn.getReturnTypeNode()),
        })
      ),
    });
  }

  return result;
}

// ---- Property extraction ----

function extractPropertySignatures(
  props: PropertySignature[]
): PropertyInfo[] {
  return props.map(extractPropertySignature);
}

function extractPropertySignature(prop: PropertySignature): PropertyInfo {
  const typeNode = prop.getTypeNode();
  return {
    name: prop.getName(),
    documentation: getDocumentation(prop),
    type: typeNode
      ? extractTypeFromNode(typeNode)
      : { kind: "intrinsic", text: prop.getType().getText() },
    isOptional: prop.hasQuestionToken(),
    isReadonly: prop.isReadonly(),
  };
}

function extractClassProperties(
  props: PropertyDeclaration[]
): PropertyInfo[] {
  return props.map((prop) => {
    const typeNode = prop.getTypeNode();
    return {
      name: prop.getName(),
      documentation: getDocumentation(prop),
      type: typeNode
        ? extractTypeFromNode(typeNode)
        : { kind: "intrinsic", text: prop.getType().getText() },
      isOptional: prop.hasQuestionToken(),
      isReadonly: prop.isReadonly(),
    };
  });
}

function extractGetAccessors(
  accessors: GetAccessorDeclaration[]
): PropertyInfo[] {
  return accessors.map((acc) => {
    const returnTypeNode = acc.getReturnTypeNode();
    return {
      name: acc.getName(),
      documentation: getDocumentation(acc),
      type: returnTypeNode
        ? extractTypeFromNode(returnTypeNode)
        : { kind: "intrinsic" as const, text: acc.getReturnType().getText() },
      isOptional: false,
      isReadonly: true, // get accessors without set are effectively readonly
    };
  });
}

// ---- Method extraction ----

function extractMethodSignatures(methods: MethodSignature[]): MethodInfo[] {
  // Group by name to collect overloads
  const methodMap = new Map<string, MethodSignature[]>();
  for (const method of methods) {
    const name = method.getName();
    const existing = methodMap.get(name);
    if (existing) {
      existing.push(method);
    } else {
      methodMap.set(name, [method]);
    }
  }

  const result: MethodInfo[] = [];
  for (const [name, overloads] of methodMap) {
    const primary = overloads[0];
    const rest = overloads.slice(1);

    result.push({
      name,
      documentation: getDocumentation(primary),
      typeParameters: extractTypeParameters(primary.getTypeParameters()),
      parameters: primary.getParameters().map(extractParameter),
      returnType: extractReturnType(primary.getReturnTypeNode()),
      overloads: rest.map(
        (m): MethodOverloadInfo => ({
          documentation: getDocumentation(m),
          typeParameters: extractTypeParameters(m.getTypeParameters()),
          parameters: m.getParameters().map(extractParameter),
          returnType: extractReturnType(m.getReturnTypeNode()),
        })
      ),
      isStatic: false,
    });
  }

  return result;
}

function extractClassMethods(methods: MethodDeclaration[]): MethodInfo[] {
  // Group by name to collect overloads
  const methodMap = new Map<string, MethodDeclaration[]>();
  for (const method of methods) {
    const name = method.getName();
    const existing = methodMap.get(name);
    if (existing) {
      existing.push(method);
    } else {
      methodMap.set(name, [method]);
    }
  }

  const result: MethodInfo[] = [];
  for (const [name, overloads] of methodMap) {
    const primary = overloads[0];
    const rest = overloads.slice(1);

    result.push({
      name,
      documentation: getDocumentation(primary),
      typeParameters: extractTypeParameters(primary.getTypeParameters()),
      parameters: primary.getParameters().map(extractParameter),
      returnType: extractReturnType(primary.getReturnTypeNode()),
      overloads: rest.map(
        (m): MethodOverloadInfo => ({
          documentation: getDocumentation(m),
          typeParameters: extractTypeParameters(m.getTypeParameters()),
          parameters: m.getParameters().map(extractParameter),
          returnType: extractReturnType(m.getReturnTypeNode()),
        })
      ),
      isStatic: primary.isStatic(),
    });
  }

  return result;
}

// ---- Parameter extraction ----

function extractParameter(param: ParameterDeclaration): ParameterInfo {
  const typeNode = param.getTypeNode();
  return {
    name: param.getName(),
    documentation: getParameterDocumentation(param),
    type: typeNode
      ? extractTypeFromNode(typeNode)
      : { kind: "intrinsic", text: param.getType().getText() },
    isOptional: param.isOptional(),
    isRestParameter: param.isRestParameter(),
  };
}

// ---- Type parameter extraction ----

function extractTypeParameters(
  params: TypeParameterDeclaration[]
): TypeParameterInfo[] {
  return params.map((p) => {
    const constraintNode = p.getConstraint();
    const defaultNode = p.getDefault();
    return {
      name: p.getName(),
      constraint: constraintNode
        ? extractTypeFromNode(constraintNode)
        : undefined,
      default: defaultNode ? extractTypeFromNode(defaultNode) : undefined,
    };
  });
}

// ---- Type extraction (structured decomposition) ----

function extractTypeFromNode(typeNode: TypeNode): TypeInfo {
  // Union type
  if (Node.isUnionTypeNode(typeNode)) {
    const types = typeNode.getTypeNodes().map(extractTypeFromNode);
    return { kind: "union", types };
  }

  // Intersection type
  if (Node.isIntersectionTypeNode(typeNode)) {
    const types = typeNode.getTypeNodes().map(extractTypeFromNode);
    return { kind: "intersection", types };
  }

  // Array type (T[])
  if (Node.isArrayTypeNode(typeNode)) {
    const elementType = extractTypeFromNode(typeNode.getElementTypeNode());
    return { kind: "array", elementType };
  }

  // Tuple type
  if (Node.isTupleTypeNode(typeNode)) {
    const elementTypes = typeNode.getElements().map(extractTypeFromNode);
    return { kind: "tuple", elementTypes };
  }

  // Literal types (string, number, boolean literals)
  if (Node.isLiteralTypeNode(typeNode)) {
    const literal = typeNode.getLiteral();
    if (Node.isStringLiteral(literal)) {
      return { kind: "literal", value: literal.getLiteralValue() };
    }
    if (Node.isNumericLiteral(literal)) {
      return { kind: "literal", value: literal.getLiteralValue() };
    }
    if (Node.isTrueLiteral(literal)) {
      return { kind: "literal", value: true };
    }
    if (Node.isFalseLiteral(literal)) {
      return { kind: "literal", value: false };
    }
    if (Node.isNullLiteral(literal)) {
      return { kind: "primitive", name: "null" };
    }
    // PrefixUnaryExpression for negative numbers
    if (Node.isPrefixUnaryExpression(literal)) {
      return { kind: "intrinsic", text: literal.getText() };
    }
    return { kind: "intrinsic", text: typeNode.getText() };
  }

  // Type reference (named types with optional type arguments)
  if (Node.isTypeReference(typeNode)) {
    const typeName = typeNode.getTypeName();
    const name = typeName.getText();
    const typeArgs = typeNode.getTypeArguments().map(extractTypeFromNode);

    // Special handling for ReadonlyArray<T> -> array
    if (name === "ReadonlyArray" && typeArgs.length === 1) {
      return {
        kind: "typeOperator",
        operator: "readonly",
        type: { kind: "array", elementType: typeArgs[0] },
      };
    }

    // Special handling for Array<T> -> array
    if (name === "Array" && typeArgs.length === 1) {
      return { kind: "array", elementType: typeArgs[0] };
    }

    // Special handling for Promise<T> and PromiseLike<T>
    return {
      kind: "reference",
      name,
      typeArguments: typeArgs,
    };
  }

  // Function type ((...) => T)
  if (Node.isFunctionTypeNode(typeNode)) {
    return {
      kind: "function",
      typeParameters: extractTypeParameters(typeNode.getTypeParameters()),
      parameters: typeNode.getParameters().map(extractParameter),
      returnType: extractReturnType(typeNode.getReturnTypeNode()),
    };
  }

  // Parenthesized type
  if (Node.isParenthesizedTypeNode(typeNode)) {
    return extractTypeFromNode(typeNode.getTypeNode());
  }

  // Type literal / object literal type
  if (Node.isTypeLiteral(typeNode)) {
    const properties = typeNode
      .getProperties()
      .map(extractPropertySignature)
      .sort(sortByName);
    const indexSignatures = typeNode
      .getIndexSignatures()
      .map(extractIndexSignature)
      .sort(sortByKeyName);
    const callSignatures = typeNode
      .getCallSignatures()
      .map(extractCallSignature);

    return {
      kind: "objectLiteral",
      properties,
      indexSignatures,
      callSignatures,
    };
  }

  // Indexed access type (T[K])
  if (Node.isIndexedAccessTypeNode(typeNode)) {
    return {
      kind: "indexedAccess",
      objectType: extractTypeFromNode(typeNode.getObjectTypeNode()),
      indexType: extractTypeFromNode(typeNode.getIndexTypeNode()),
    };
  }

  // Type operator (keyof T, readonly T[], typeof T)
  // ts-morph does not expose Node.isTypeOperator, so use SyntaxKind check
  if (typeNode.getKind() === SyntaxKind.TypeOperator) {
    // Cast to access TypeOperatorTypeNode methods (no Node.isTypeOperator guard)
    const typeOpNode = typeNode as TypeOperatorTypeNode;
    const operatorToken = typeOpNode.getOperator();
    let operator: string;
    switch (operatorToken) {
      case SyntaxKind.KeyOfKeyword:
        operator = "keyof";
        break;
      case SyntaxKind.ReadonlyKeyword:
        operator = "readonly";
        break;
      case SyntaxKind.UniqueKeyword:
        operator = "unique";
        break;
      default:
        operator = "unknown";
    }
    return {
      kind: "typeOperator",
      operator,
      type: extractTypeFromNode(typeOpNode.getTypeNode()),
    };
  }

  // Conditional type (A extends B ? C : D)
  if (Node.isConditionalTypeNode(typeNode)) {
    return { kind: "conditional", text: typeNode.getText() };
  }

  // Mapped type
  if (Node.isMappedTypeNode(typeNode)) {
    return { kind: "intrinsic", text: typeNode.getText() };
  }

  // Template literal type
  if (Node.isTemplateLiteralTypeNode(typeNode)) {
    return { kind: "intrinsic", text: typeNode.getText() };
  }

  // Infer type
  if (Node.isInferTypeNode(typeNode)) {
    return { kind: "intrinsic", text: typeNode.getText() };
  }

  // TypeQuery (typeof X)
  if (Node.isTypeQuery(typeNode)) {
    return {
      kind: "typeOperator",
      operator: "typeof",
      type: { kind: "reference", name: typeNode.getExprName().getText(), typeArguments: [] },
    };
  }

  // Rest type
  if (Node.isRestTypeNode(typeNode)) {
    return { kind: "intrinsic", text: typeNode.getText() };
  }

  // Named tuple member
  if (Node.isNamedTupleMember(typeNode)) {
    return extractTypeFromNode(typeNode.getTypeNode());
  }

  // Keyword types (string, number, boolean, void, any, etc.)
  const text = typeNode.getText();
  const primitives = new Set([
    "string",
    "number",
    "boolean",
    "void",
    "null",
    "undefined",
    "any",
    "unknown",
    "never",
    "bigint",
    "symbol",
    "object",
    "this",
  ]);

  if (primitives.has(text)) {
    return { kind: "primitive", name: text };
  }

  // Fallback: use text representation
  return { kind: "intrinsic", text };
}

function extractReturnType(returnTypeNode: TypeNode | undefined): TypeInfo {
  if (returnTypeNode) {
    return extractTypeFromNode(returnTypeNode);
  }
  return { kind: "primitive", name: "void" };
}

// ---- Index signature extraction ----

function extractIndexSignature(
  sig: IndexSignatureDeclaration
): IndexSignatureInfo {
  const keyParam = sig.getKeyName();
  const keyTypeNode = sig.getKeyTypeNode();
  const returnTypeNode = sig.getReturnTypeNode();

  return {
    keyName: keyParam,
    keyType: extractTypeFromNode(keyTypeNode),
    valueType: returnTypeNode
      ? extractTypeFromNode(returnTypeNode)
      : { kind: "primitive", name: "any" },
    isReadonly: sig.isReadonly(),
  };
}

// ---- Call signature extraction ----

function extractCallSignature(
  sig: CallSignatureDeclaration
): CallSignatureInfo {
  return {
    documentation: getDocumentation(sig),
    typeParameters: extractTypeParameters(sig.getTypeParameters()),
    parameters: sig.getParameters().map(extractParameter),
    returnType: extractReturnType(sig.getReturnTypeNode()),
  };
}

// ---- Type reference from extends/implements expression ----

function extractTypeReferenceFromExpression(
  expr: Node
): TypeReference {
  if (Node.isExpressionWithTypeArguments(expr)) {
    const typeArgs = expr.getTypeArguments().map(extractTypeFromNode);
    return {
      name: expr.getExpression().getText(),
      typeArguments: typeArgs,
    };
  }

  return {
    name: expr.getText(),
    typeArguments: [],
  };
}

// ---- Documentation helpers ----

function getDocumentation(node: Node): string | undefined {
  if (!Node.isJSDocable(node)) {
    return undefined;
  }

  const jsDocs = node.getJsDocs();
  if (jsDocs.length === 0) {
    return undefined;
  }

  // Combine all JSDoc comments
  const parts: string[] = [];
  for (const doc of jsDocs) {
    const comment = doc.getComment();
    if (comment) {
      if (typeof comment === "string") {
        parts.push(comment);
      } else if (Array.isArray(comment)) {
        // JSDocTag nodes - extract text
        parts.push(
          comment
            .filter((c): c is NonNullable<typeof c> => c != null)
            .map((c) => c.getText())
            .join("")
        );
      }
    }
  }

  const result = parts.join("\n").trim();
  return result || undefined;
}

function getParameterDocumentation(
  param: ParameterDeclaration
): string | undefined {
  // Try to find @param tag in the parent's JSDoc
  const parent = param.getParent();
  if (!parent || !Node.isJSDocable(parent)) {
    return undefined;
  }

  const paramName = param.getName();
  const jsDocs = parent.getJsDocs();

  for (const doc of jsDocs) {
    const tags = doc.getTags();
    for (const tag of tags) {
      if (
        Node.isJSDocParameterTag(tag) &&
        tag.getName() === paramName
      ) {
        const comment = tag.getComment();
        if (typeof comment === "string") {
          return comment.trim() || undefined;
        }
      }
    }
  }

  return undefined;
}

// ---- Sorting helpers ----

function sortByName<T extends { name: string }>(a: T, b: T): number {
  return a.name.localeCompare(b.name);
}

function sortByKeyName<T extends { keyName: string }>(a: T, b: T): number {
  return a.keyName.localeCompare(b.keyName);
}
