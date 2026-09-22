# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).
Versionado según [SemVer 2.0](https://semver.org/lang/es/).

Antes de la v1.0, las versiones minor pueden introducir cambios de ruptura.

## [Unreleased]

### Added

- Documentación de planeación completa (`docs/`), incluidos 4 ADRs.
- Esqueleto de la solución: multi-targeting `netstandard2.0;net8.0;net9.0`, Central Package
  Management, `.editorconfig` con estilo obligatorio y warnings como errores.
- Contratos base: `IMapper`, jerarquía de excepciones y `TypeMapKey` del modelo de configuración.
- Nombre definitivo fijado: **Mapperion** (D-01 cerrada tras verificar disponibilidad en NuGet,
  GitHub y colisiones de producto).
- Target framework `net10.0` añadido al core y a los tests.
- Modelo de configuración (capa 2): `MapperModel`, `MapperOptions`, `TypeMapDefinition`,
  `MemberDefinition`, `ConstructorParameterDefinition`, `MemberSource` y sus cuatro variantes,
  `MemberPath`, `MemberDescriptor` y las enumeraciones de política.
- `Internal/Guard` centraliza las comprobaciones de nulos y el `#if` que exige `netstandard2.0`.
- ADR-0005: acota qué comparten de verdad el motor runtime y el source generator, corrigiendo
  ADR-0001 y el documento de arquitectura.
- API fluida (capa 1): `MapperConfiguration`, `IMapperConfigurationExpression`,
  `IMappingExpression<,>` e `IMemberConfigurationExpression<,,>`, con `CreateMap`, `ForMember`,
  `MapFrom`, `Ignore`, `Condition`, `NullSubstitute`, `SetMappingOrder`, `UseDestinationValue`,
  `ValidateMemberList`, `MaxDepth` y `PreserveReferences`.
- `MemberExpressionParser` traduce las lambdas a elementos del modelo: una cadena de miembros se
  guarda como `MemberPathSource` y cualquier otra expresión como `CustomSource` opaco.
- Convenciones (capa 3): coincidencia por nombre exacto, sin distinguir mayúsculas, con prefijos y
  sufijos configurables, y aplanado hasta `MaxFlatteningDepth`. La configuración explícita siempre
  gana y un miembro sin origen queda con `Source` nulo para que la validación lo reporte.
- `Profile`, `AddProfile<T>()`, `AddProfile(instancia)` y `AddProfiles(ensamblados)`, adelantados
  desde v0.2 por ser el mayor bloqueo de migración desde AutoMapper.
- Los caminos que usan reflexión están anotados con `[RequiresUnreferencedCode]`, incluido el
  constructor de `MapperConfiguration`. Se retira `IsAotCompatible` del core: era una afirmación
  falsa mientras el motor dependa de reflexión y de `Expression.Compile`.
- `Internal/TrimmingAttributes.cs` aporta el polyfill de `RequiresUnreferencedCodeAttribute` para
  `netstandard2.0`, que PolySharp no cubre.
- La carpeta `docs/` queda fuera del repositorio.
- `AssertIsValid()` y el alias `AssertConfigurationIsValid()`: reportan todos los problemas juntos
  en `MapperConfigurationException.Errors`, nunca solo el primero. Detectan miembros destino sin
  origen, mapas anidados que faltan (desenvolviendo nullables y colecciones) y, con
  `MemberListValidation.Source`, miembros de origen que nadie consume.
- `Internal/TypeClassifier`: distingue tipos simples, nullables y secuencias.
- `ValidateOnBuild` pasa a `false` por defecto, como AutoMapper, para no romper en arranque las
  configuraciones recién migradas.
- `MemberListValidation` global ahora se aplica de verdad a los mapas que no lo sobrescriben.
- Compilador de expresiones (capa 4) y motor de ejecución (capa 5): `MapperConfiguration.CreateMapper()`
  devuelve un `IMapper` con `Map<TDest>(object)`, `Map<TSource,TDest>(source)`,
  `Map(source, destino)` y la sobrecarga no genérica.
- Los planes se compilan la primera vez que se usa cada par y se cachean. Los mapas anidados se
  resuelven en ejecución en vez de insertarse en línea, que es lo que permite que dos mapas se
  referencien mutuamente sin que el compilador recurse.
- Conversiones: nullables en ambos sentidos, numéricas, enums según la política configurada,
  enum con string, `ToString` e `IConvertible` como último recurso.
- Colecciones: array, `List<>`, `HashSet<>`, `ISet<>` y las interfaces de secuencia, con
  `AllowNullCollections` respetado.
- Rutas aplanadas con guardas de nulo que evalúan cada paso una sola vez.
- `Condition`, `NullSubstitute`, `SetMappingOrder` y `UseDestinationValue` llegan al código generado.
- Mapeo por constructor: records posicionales, constructores primarios y cualquier destino sin
  constructor sin parámetros. Se elige la sobrecarga con más argumentos resolubles; los parámetros
  con valor por defecto cubren lo que el origen no aporta.
- `ForCtorParam(nombre, o => o.MapFrom(...))` y `UseValue(...)` para configurar un argumento a mano.
- Los nombres de parámetro se comparan siempre sin distinguir mayúsculas: C# nombra los parámetros
  en camelCase y las propiedades en PascalCase, así que una comparación exacta nunca emparejaría el
  constructor primario de un record con las propiedades del origen.
- Un miembro alimentado por el constructor ya no se asigna otra vez después de construir.
- La validación reporta los parámetros de constructor sin origen y los mapas que les faltan.
