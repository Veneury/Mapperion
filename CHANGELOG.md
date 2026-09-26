# Changelog

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/).
Versionado según [SemVer 2.0](https://semver.org/lang/es/).

Antes de la v1.0, las versiones minor pueden introducir cambios de ruptura.

## [Unreleased]

### Added

- `string` a `Guid`, `DateOnly` y `TimeOnly`. Un identificador o una fecha que llegan como texto
  —de un JSON, o de una columna que alguien escribió como `varchar`— es lo primero que se encuentra
  cualquiera. `string` a `DateTime` ya funcionaba porque `DateTime` es `IConvertible` y estos tres
  no lo son, una distinción que no le dice nada a quien escribe el mapa.
- El texto vacío da el valor por defecto, igual que ya hacía con los enums: ausente no es lo mismo
  que mal escrito. El texto que sí pretende ser un valor y no lo es lanza una excepción que lo
  nombra, porque lo útil es saber qué fila traía la basura.
- Se lee con la cultura invariante. Un mapeo que entendiera la misma fecha de forma distinta según
  la máquina sería peor problema que el que resuelve.
- Se mira **después** de un mapa declarado, así que `CreateMap<string, Guid>()` sigue ganando si
  alguien quiere su propia lectura.

## [0.10.0] - 2026-09-26

La primera sin sufijo `-preview`. No porque la API haya dejado de moverse —antes de la 1.0 una
minor puede seguir rompiendo, y lo dice la línea de arriba— sino porque NuGet no enseña las
pre-release en las búsquedas ni las instala sin pedirlas por número, y una librería que nadie
encuentra no recibe la única cosa que le falta, que es gente usándola.

Una minor y no un parche: `ProjectTo` cambia lo que devuelve para los enums que no cruzan por
número. Es un arreglo, pero cambia una respuesta.

### Added

- Un proyecto piloto, `samples/Mapperion.Bookshop`: una aplicación pequeña que usa la librería como
  la usa una aplicación, no como la usa un test. Contenedor, dos perfiles, EF Core sobre SQLite,
  `AssertIsValid()` al arrancar, una vista de lista por `ProjectTo` y una de detalle mapeada en
  memoria. Comprueba su propia salida y sale con código distinto de cero si algo no cuadra, así que
  la CI lo corre como una prueba más.
- Lo que cubre no es ninguna cosa rara por separado: es todo a la vez sobre una sola
  configuración, que es justo donde una suite de tests unitarios menos mira.

- `Mapperion.Analyzers`, un paquete aparte y opcional que lee la configuración en tiempo de
  compilación. Cuatro reglas: **MPR1001** el mismo par declarado dos veces, **MPR1002** un miembro
  con dos orígenes, **MPR1003** un miembro ignorado y con origen a la vez, **MPR1004**
  `ConstructUsing` junto a `ForCtorParam`.
- Va en su propio paquete para que `Mapperion` siga sin ninguna dependencia, que es de lo poco que
  puede decir que casi nadie más dice. No lleva código de runtime y nada suyo llega a tu salida.
- Todo son avisos, ninguno error. Dos de los cuatro describen algo que revienta al construir la
  configuración, pero esos mismos dos se pueden escribir en ramas distintas de un `if` donde solo
  corre uno. Quien quiera que paren el build ya convierte los avisos en errores.
- Lo que **no** hace: decirte si un miembro del destino va a encontrar origen. Para eso está
  `AssertIsValid()`. Contestarlo aquí sería una segunda copia del motor de convenciones al lado de
  la primera, separándose de ella con el tiempo, y una respuesta equivocada de un analizador es
  peor que ninguna.
- Todo se reconoce por símbolo y no por nombre: un `ForMember` del builder fluido de otra librería
  se queda en paz. Hay una prueba que dice exactamente eso.
- Corrido sobre la propia suite de la librería: 320 pruebas y tres avisos, los tres en pruebas
  escritas a propósito para comprobar ese fallo. Quedan silenciados en esos tres sitios con un
  `#pragma` que dice por qué, y el analizador sigue mirando todo lo demás.
- La primera versión de MPR1002 decía que configurar un miembro dos veces dejaba solo el último con
  efecto. Es falso: las opciones se acumulan, y una prueba de la librería ya lo decía. La regla
  ahora habla solo del origen, que es lo único que de verdad se reemplaza.

- `ProjectTo` sobre Entity Framework 6. Resultó que ya funcionaba casi entero —es el mismo método
  del core, que no depende de ningún ORM—, salvo por un detalle: la proyección emitía nodos
  `Expression.Default` para el valor de un miembro que no se puede leer, y el traductor de EF6 se
  planta con «Unknown LINQ expression of type 'Default'». EF Core sí los acepta, así que nunca
  había salido. Ahora emite una constante, que entienden los dos y cualquier otro proveedor.
- Suite propia en `tests/Mapperion.EntityFramework6.Tests`, sobre net472 y sin base de datos: EF6
  genera el SQL desde su modelo y `ToString()` sobre la consulta lo devuelve, así que el SQL es la
  aserción y no hace falta un servidor en el build.
- Los cuatro benchmarks que faltaban: B06 enums, B08 polimorfismo, B09 `ProjectTo` sobre EF Core y
  SQLite, y B11 dieciséis hilos a la vez. Con eso la tabla del doc 07 está completa.
- B08 y B11 salieron bien: el polimorfismo va a 4,69x del manual (2,81x con `MapFast`) contra 8,75x
  de AutoMapper, y con dieciséis trabajadores el múltiplo *mejora* a 1,73x, o sea que no hay
  cerrojo ni estado por instancia que haga cola. B09 empata con todos porque el tiempo es de EF y
  de SQLite; lo que detecta es la proyección que se cae al cliente, que no sería un empate.
- B06 encontró algo, lo de los enums por nombre, y también está arreglado más abajo.
- B08 encontró otra, la del destino base abstracto, y esa ya está arreglada más abajo.
- Polimorfismo y enums por valor entran en el presupuesto de CI. Los otros tres no, y por motivos:
  el de nombre es un coste conocido y no un suelo que defender, el de proyección necesita base de
  datos, y el de concurrencia depende de cuántos núcleos tenga el runner.
- Convenciones de nombres configurables, con la forma de AutoMapper: `SourceMemberNamingConvention`
  y `DestinationMemberNamingConvention`, más `PascalCaseNamingConvention`,
  `LowerUnderscoreNamingConvention` y `ExactMatchNamingConvention`. Un origen que escribe
  `first_name` y un destino que escribe `FirstName` ya se encuentran sin un `ForMember` por
  propiedad. Ignorar mayúsculas no bastaba: se diferencian en un carácter, no en la caja.
- El aplanado cruza las dos grafas, así que `ShipToCityName` llega a `ship_to.city_name`. Y
  `ExactMatchNamingConvention` como convención de origen apaga el aplanado, igual que en
  AutoMapper.
- Los valores por defecto dejan el nombre tal cual, así que ninguna configuración existente
  cambia de comportamiento ni paga nada por esto.
- `Explain()` describe, miembro a miembro, en qué quedó un mapa y de dónde sale cada valor. Separa
  lo configurado a mano de lo que decidió una convención, que es de donde viene casi toda la
  confusión cuando un miembro trae algo inesperado, y deja bien visible el que se quedó sin origen,
  que es por lo que uno mira esto en primer lugar. Hay tres formas: por tipos genéricos, por
  `Type`, y sin argumentos para todos los mapas declarados.
- Es texto para leer, no para parsear: la redacción cambiará cuando aparezca una mejor. No ejecuta
  ningún mapeo, solo lee el modelo ya construido.
- Presupuesto de regresión de rendimiento en la CI. Los tests dicen qué devuelve un mapeo; nada
  decía cuánto tarda, y las dos cosas se separan con facilidad: un cambio puede dejar todos los
  resultados idénticos y duplicar el tiempo con la suite entera en verde. Estuvo a punto de pasar
  al escribir la opción de la ruta de error, y solo lo evitó releer el código.
- Se compara el múltiplo sobre el mapeo a mano medido en la misma corrida, nunca el tiempo: un
  runner compartido mueve las dos cifras a la vez, así que la proporción aguanta lo que los
  nanosegundos no. La línea base está en `benchmarks/baseline.json` y se sube a mano.
- Comprobado en los dos sentidos: con el árbol limpio pasa, y desactivando el inlining a propósito
  falla en la colección y en el anidado diciendo cuál y cuánto.

### Fixed

- **Una proyección cruzaba los enums por número mientras el motor los cruzaba por nombre.** El
  mismo pedido salía `Shipped` en la vista de detalle y `Placed` en la de lista, con un solo
  `CreateMap` y una sola política detrás. Lo encontró el piloto a los diez minutos de existir.
- La proyección emite ahora la correspondencia como una cadena de condiciones que el proveedor
  convierte en un `CASE`, sacada de la misma tabla que usa el motor de mapeo —ahora en
  `EnumCorrespondence`, compartida por los dos— para que no puedan volver a discrepar. EF Core y
  EF6 la traducen las dos, y hay pruebas en ambas suites que lo dicen, incluida una que comprueba
  que el `CASE` lo hace la base de datos y no el cliente.
- Lo único que una proyección no puede hacer es lanzar el error que `ByName` lanza en memoria para
  un valor sin contrapartida: nada nuestro corre por fila, el SQL tiene brazo para un valor o no lo
  tiene. Un valor fuera de los declarados cae a su número, que es lo que hacía antes.
- Mapear enums **por nombre** costaba 25x el mapeo a mano y asignaba 280 B donde el manual asigna
  40. El nombre se resolvía en cada llamada: un `ToString()` para sacarlo del origen, un
  `Enum.TryParse` contra el destino y otro `ToString()` para confirmar que el nombre volvía igual
  — por miembro y por mapeo.
- Los dos tipos se conocen al compilar el plan, así que la correspondencia se resuelve ahí una sola
  vez y se emite como un `switch`. **4,56x y 40 B**, las mismas asignaciones que el manual.
- Las respuestas no cambian, ninguna. Solo entran como caso los miembros que se pueden resolver en
  compilación; lo demás —un valor fuera de los declarados, una combinación de flags, un nombre que
  el destino no tiene bajo `ByName`— cae al `default`, que es la misma llamada de runtime de antes,
  con la misma excepción y el mismo mensaje. Las once pruebas nuevas se escribieron contra la
  implementación nueva y se pasaron también contra la vieja, que es lo que demuestra que solo
  cambió la velocidad.
- Mapear enums **por valor** costaba 15x el mapeo a mano y asignaba 400 B, diez veces lo que asigna
  el manual: era la peor de las dos rutas, no la buena. El número se llevaba al otro lado con un
  `Convert.ToInt64` y un `Enum.ToObject`, que boxean dos veces por miembro, cuando llevarlo es
  exactamente lo que hace una conversión. Ahora se emite como tal: **de 82,7 ns a 20,6 ns y de
  400 B a 40 B**, las mismas asignaciones que el manual.
- De paso desaparece un fallo que nadie había visto: pasar el valor por un `Int64` hacía que un enum
  `ulong` con un valor por encima de `long.MaxValue` lanzara `OverflowException`, incluso llevándolo
  a un enum de exactamente la misma forma, donde no se estaba estrechando nada. Comprobado sobre las
  64 combinaciones de tipos subyacentes: 56 idénticas, y las 8 que cambian son todas ese caso.
- Las dos mitades de B06 entran en el presupuesto de CI. Ninguna lo merecía antes: a 25x y 15x,
  fijarlas no habría protegido nada.

- Un mapa polimórfico cuyo destino base es **abstracto** no llegaba a compilar. El plan exigía poder
  construir el destino aunque el mapa tuviera `Include` para todos los tipos concretos, y una clase
  abstracta no tiene constructor público sin parámetros, así que saltaba
  `MapperConfigurationException` al primer mapeo. AutoMapper acepta esa misma configuración, con lo
  que era un bloqueo de migración directo: quien tenga una jerarquía de DTOs con base abstracta
  —que es la forma normal de tenerla— no podía pasarse.
- Ahora, cuando el mapa tiene derivados, esa construcción se emite como una excepción de tiempo de
  mapeo en vez de rechazarse al compilar. Solo se llega a ella si ningún derivado coincidió, y
  entonces dice qué tipo llegó y qué hay que declarar. Sin derivados, un destino que no se puede
  construir sigue siendo un error de configuración, que es lo que es. Y si quien llama trae su
  propia instancia de destino, se escribe en ella como siempre: no hay nada que construir.
- El benchmark B08 volvió a la base abstracta, que es la forma real, y mide lo mismo que con la
  base concreta.

- La restauración de la solución fallaba en Linux desde que entraron los tests de EF6. El proyecto
  se dejaba sin ningún target framework fuera de Windows, y NuGet no restaura un proyecto así: el
  build se caía con un `MSB4181` que no nombra ni el proyecto ni el motivo. Fuera de Windows ahora
  es un ensamblado vacío que restaura, no compila nada y no es proyecto de tests, así que `dotnet
  test` no lo mira. En Windows sigue siendo net472 con sus cinco pruebas.

### Changed

- **El benchmark B06 por valor estaba mal y sus números publicados también.** Le había dado al
  destino los mismos tipos de enum que al origen, y la conversión ni siquiera los mira —tipos
  idénticos se asignan directamente—, así que cronometraba cinco asignaciones. Se ve en la columna
  de AutoMapper, que pasa de 10,06x a 39,95x sin haber cambiado: antes no estaba convirtiendo nada.
  Las dos mitades usan ahora el mismo destino con tipos distintos y solo las separa la política.
- También estaba mal lo que dije la versión pasada sobre que por nombre fuera más rápido que por
  valor. Comparé dos múltiplos con suelos distintos: cinco casts a mano cuestan 4,0 ns y cinco
  `switch` a mano 5,7 ns, así que el múltiplo mayor de por valor (5,21x contra 4,56x) convive con
  ser más rápido en absoluto (20,6 ns contra 25,7 ns).

## [0.9.0-preview.1] - 2026-09-23

Una minor y no un parche porque el nombre seguro cambia la identidad de los ensamblados, que es
una ruptura. Antes de la 1.0 está permitido, y es cuando sale más barato.

### Added

- Las tres cajas llevan icono, así que NuGet deja de mostrar el marcador genérico.
- La superficie pública está escrita en `PublicAPI.Shipped.txt` junto a cada proyecto que se
  publica, y el build compara las dos cosas: añadir, quitar o cambiar algo público rompe la
  compilación hasta que el fichero se actualiza, con lo cual aparece en el diff. Un solo fichero
  cubre los seis TFMs, porque la superficie es idéntica en todos.
- `CONTRIBUTING.md`, con qué hacer cuando el build falla por eso y las reglas de la casa.
- Sitio de documentación en `website/`, generado con DocFX y publicado en GitHub Pages desde
  `main`. La referencia de API sale de la documentación XML que el build ya exige en cada miembro
  público, así que no puede desviarse del código. Cuatro artículos escritos a mano: primeros pasos,
  migración desde AutoMapper, AOT y rendimiento. El fuente vive en `website/` y no en `docs/`,
  que está fuera del repositorio.
- `GOVERNANCE.md`, `SECURITY.md` y `CODE_OF_CONDUCT.md`. El de gobernanza explica por qué el
  compromiso de licencia vale algo: no hay CLA ni cesión de copyright, así que relicenciar
  versiones futuras haría falta el acuerdo de todo el que haya contribuido, y las ya publicadas
  quedan MIT para siempre pase lo que pase. También dice dónde esa protección todavía es floja,
  que es hoy, con un solo contribuidor.
- Todos los ensamblados llevan nombre seguro, con la clave `mapperion.snk`, que está en el
  repositorio. Sin esto una base de código firmada en .NET Framework no puede referenciar
  Mapperion en absoluto, y .NET Framework es un objetivo que esta librería se toma en serio. La
  clave va versionada a propósito: un nombre seguro es identidad, no seguridad, y cualquiera puede
  quitarlo y volver a firmar con la suya.

### Changed

- **Cambia la identidad de los ensamblados**, que ahora llevan el token `03d4952d6f16ebf2`. Quien
  referenciara la 0.8.0-preview.3 verá un ensamblado distinto al actualizar. Se hace ahora, días
  después de publicar por primera vez y antes de la 1.0, porque más adelante saldría mucho más
  caro.
- La firma de autor de los paquetes sigue sin hacerse, y no es cuestión de trabajo: nuget.org exige
  un certificado de firma de código que encadene a una raíz de confianza y rechaza los autoemitidos.
  Lo que sí hay, sin coste, es que nuget.org firma como repositorio todo lo que acepta.

## [0.8.0-preview.3] - 2026-09-23

Primera versión pública. Todo lo de abajo se acumuló antes de publicar nada, así que esta
entrada es larga por una vez; las siguientes no lo serán.

### Added

- `MapFast`, un método de extensión sobre `IMapper` que hace el mismo mapeo evitando el coste de
  llamar a un método genérico a través de una interfaz. Reconoce el mapper que construye la
  librería y lo llama directamente; con cualquier otra implementación, como un decorador, cae de
  vuelta a la interfaz y sigue funcionando. No cambia nada de `IMapper`, así que el `Map` de
  siempre queda igual.
- En el escenario plano baja de 28,1 ns a 18,7 ns, de 2,75x a 1,83x sobre el mapeo a mano, que
  empata con Mapster dentro de las barras de error. El ahorro es un coste fijo por llamada, así
  que cuanto más trabajo tenga el mapeo menos pesa. Para un camino realmente caliente el source
  generator sigue siendo mejor respuesta: está en 0,93x.
- `IncludeMembers(s => s.Applicant, s => s.Employment)` construye un destino a partir de varios
  objetos anidados del origen. El mapa se mira primero: lo que configura explícitamente y lo que
  resuelven sus propias convenciones gana, y solo lo que queda sin origen se ofrece a los miembros
  incluidos, en el orden dado. El primero que tenga algo que decir lo aporta, y un miembro que el
  mapa incluido ignora cuenta como no tener nada que decir.
- Si hay un mapa declarado para el tipo incluido se usa, así que sus renombrados y sus
  `IValueConverter` viajan con él; si no lo hay, el miembro se empareja contra el tipo incluido por
  las mismas convenciones de siempre, sin obligar a declarar un mapa que nadie necesitaría.
- Un `IValueResolver` del mapa incluido recibe la instancia incluida, no el origen de fuera. Una
  condición sí se reporta en vez de descartarse: está escrita contra el tipo incluido y no hay
  forma de trasladarla al de fuera.
- Un miembro incluido a nulo deja a cero lo que habría rellenado, igual que ya hace una ruta
  aplanada con un nulo por el camino.
- `ProjectTo` atraviesa los miembros incluidos cuando lo que aportan es una ruta o una expresión.

- `samples/Mapperion.Aot`, una aplicación publicada con `PublishAot=true` que mapea con el código
  generado y comprueba su propio resultado, saliendo con código distinto de cero si algo no cuadra.
  Lleva los analizadores de trimming y AOT activados, así que un build corriente ya falla ante
  cualquier cosa que el trimmer no pueda seguir, y la CI la publica en nativo y la ejecuta. Hasta
  ahora el soporte AOT era una afirmación sin nada que la respaldara; ahora se mide en cada build.

- `ForPath(d => d.Address.Street, ...)` escribe un miembro que está dentro del destino en vez de
  sobre él. Los objetos del camino se crean si faltan; uno que no se pueda escribir y esté a nulo
  tiene que venir puesto, y el mapa dice cuál era. Las rutas se asignan después de todos los
  miembros directos, así que configurar el objeto entero y algo de dentro deja la última palabra a
  la ruta en vez de depender del orden de declaración. Una ruta de un solo paso es un miembro
  normal. Una proyección la reporta en vez de ignorarla.
- `ConstructUsing`, con las dos sobrecargas de AutoMapper: la que recibe solo el origen y la que
  además recibe el `ResolutionContext`. La fábrica solo corre cuando hay que crear el destino, así
  que mapear sobre una instancia que trae el llamante la sigue usando a ella. Declararla junto a
  `ForCtorParam` se rechaza al construir la configuración, porque la fábrica ganaría y los
  parámetros no harían nada.
- `ResolutionContext.Items`, con sobrecargas de `Map` que toman un
  `Action<IMappingOperationOptions>` para llenarlos. Sirven para pasar contexto que no está en el
  objeto de origen, como el usuario o el tenant actual. El diccionario es de una operación y no se
  comparte con otra. Solo se reserva estado cuando la configuración tiene algo que pueda leerlo:
  un converter, un resolver o un paso que reciba el contexto.
- `MapperHost`, un hueco para un `IMapper` accesible estáticamente, pensado para .NET Framework sin
  contenedor y documentado como último recurso. Instalar uno segundo sin llamar antes a `Reset` se
  rechaza, porque cambiaría a media ejecución lo que resuelven las llamadas ya escritas.

- Un grafo de objetos que se cierra sobre sí mismo ya no tumba el proceso. El mapa que cierra el
  bucle cuenta su propia profundidad y lanza `RecursionLimitException` al pasar de
  `RecursionLimit`, que por defecto son 64 niveles, el mismo valor que usan `System.Text.Json` y
  Newtonsoft para la misma protección. Antes la recursión terminaba en un `StackOverflowException`,
  que no se puede capturar y se lleva el proceso por delante: es la forma del CVE-2026-32933 de
  AutoMapper, que no se va a parchear en su línea MIT.
- Solo cuentan los mapas que cierran un bucle sin `MaxDepth` ni `PreserveReferences`, así que una
  configuración cuyos tipos no pueden recurrir no paga nada por esto, y un mapa que ya se protege
  conserva su propio comportamiento.
- `RecursionLimit` en la configuración, para subirlo cuando el grafo de verdad es más profundo.
  Cero o menos quita el techo y devuelve el desbordamiento de pila.
- `RecursionLimitException` deriva de `MappingException`, así que un `catch` existente la sigue
  atrapando, y lleva el mapa y el límite que se alcanzó.

- Herencia y polimorfismo. `Include<TDerivedSource,TDerivedDestination>()` hace que mapear a través
  de una referencia base produzca el destino derivado que corresponde; las comprobaciones se emiten
  de más derivado a menos, así que una jerarquía de varios niveles elige la coincidencia más
  cercana y no la primera que encaje.
- `IncludeBase<TBaseSource,TBaseDestination>()` toma la configuración de miembros del mapa base
  antes de que corran las convenciones. Lo que el mapa derivado configure gana.
- Una colección del tipo base mapea cada elemento a su propio tipo derivado.
- La validación reporta un `Include` o un `IncludeBase` hacia un mapa no declarado, y un `Include`
  cuyo destino derivado no hereda del destino base.
- Una proyección reporta un mapa polimórfico: la forma de una proyección se fija antes de leer
  ninguna fila, así que no puede depender del tipo en tiempo de ejecución.
- Genéricos abiertos: `CreateMap(typeof(Page<>), typeof(PageDto<>))` declara una plantilla que el
  motor cierra la primera vez que llega un par que encaja, y guarda el resultado. Funciona igual
  dentro de un `Profile` y con varios argumentos de tipo.
- Un mapa cerrado declarado a mano tiene prioridad sobre la plantilla que también encajaría.
- `IOpenMappingExpression` expone solo lo que se puede decir sin conocer los tipos:
  `IgnoreMember(nombre)`, `ValidateMemberList`, `MaxDepth` y `PreserveReferences`. Configurar un
  miembro con una expresión contra un tipo que aún no tiene argumentos no tendría sentido, así que
  los miembros quedan en manos de las convenciones al cerrar.
- Las plantillas quedan fuera de la validación de miembros y de la detección de ciclos, que no
  significan nada sobre un tipo sin cerrar. Sí se comprueba que las dos partes tengan el mismo
  número de argumentos de tipo, y que no se mezcle un tipo abierto con uno cerrado.
- Paquete `Mapperion.SourceGenerator`: un generador incremental de Roslyn que escribe el cuerpo de
  los métodos `partial` de una clase marcada con `[Mapper]`. La salida es C# corriente, sin
  reflexión y sin emisión de código en ejecución, que es lo que la hace válida bajo trimming y AOT.
- Emparejamiento por nombre exacto y luego sin distinguir mayúsculas, igual que el motor de
  runtime; `[MapProperty("Customer.Address.City", "CustomerCity")]` para rutas explícitas, con
  guarda de nulos en cada paso; `[MapperIgnore]` para saltarse un miembro.
- Cubre objetos anidados llamando a otro método del mismo mapeador, colecciones con `Select` y
  `ToList`/`ToArray`/`ToHashSet`, nullables, enums, conversiones numéricas, `ToString` y
  construcción por constructor, incluidos los records.
- Seis diagnósticos, `MPR0001` a `MPR0006`, para lo que no puede escribir: clase no `partial`,
  miembro sin origen, conversión inexistente, destino que no se puede construir, firma no
  soportada, y atributo que nombra un miembro inexistente.
- Los atributos los emite el propio generador en cada compilación, `internal`, así que el paquete
  no arrastra dependencia en ejecución y dos ensamblados nunca chocan.
- `BothEnginesAgreeTests` pasa los mismos casos por los dos motores y compara los resultados. Es la
  garantía que ADR-0005 dejó como condición: los dos no comparten una línea de código, así que lo
  único que los mantiene honestos es ejecutarlos contra lo mismo.

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
- `ReverseMap()`: declara el par inverso y lo devuelve para seguir configurándolo. Invierte los
  miembros renombrados con un `MapFrom` sobre un único miembro escribible; las rutas aplanadas, las
  expresiones arbitrarias y los miembros ignorados no se invierten y el inverso los resuelve por
  convención. Funciona igual dentro de un `Profile`.
- Workflow de CI: build y test en Linux y Windows contra .NET 8, 9 y 10, y `pack` del core.
- `ITypeConverter<TSource,TDest>`, `IValueConverter<TSourceMember,TDestMember>` e
  `IValueResolver<TSource,TDest,TMember>`, con `ResolutionContext` como vía de vuelta al mapper
  para que el código de usuario pueda mapear valores anidados.
- `ConvertUsing<TTypeConverter>()` sustituye el mapa completo de un par de tipos; en ese caso la
  configuración de miembros deja de aplicarse y la validación de miembros se omite.
- `ConvertUsing<TValueConverter, TSourceMember>()` y `MapFrom<TValueResolver>()` por miembro,
  ambos con restricciones de tipo, así que el compilador de C# rechaza un converter que no encaje.
- Converters y resolvers se instancian una sola vez y se reutilizan, cacheados por el motor. De
  momento necesitan un constructor sin parámetros; la resolución por DI llega en v0.4.
- Un resolver que alimenta un parámetro de constructor recibe el destino por defecto, porque
  todavía no existe cuando se calculan los argumentos.
- `BeforeMap` y `AfterMap`, cada uno en tres formas: lambda de dos argumentos, lambda con
  `ResolutionContext`, y `IMappingAction<TSource,TDestination>` como tipo propio.
- Los pasos se insertan en el plan compilado en el orden en que se declararon: los de antes justo
  después de crear el destino, los de después una vez asignados todos los miembros. Los pasos con
  tipo propio se instancian una sola vez, igual que converters y resolvers.
- Un mapa con `ConvertUsing` no ejecuta los pasos: el converter reemplaza el mapa entero, igual que
  reemplaza la configuración de miembros.
- Paquete `Mapperion.Extensions.DependencyInjection` con `AddMapperion(...)`, en tres formas:
  callback de configuración, ensamblados a escanear, o tipos marcadores.
- `MapperConfiguration.CreateMapper(IServiceProvider)`: el mapper pide converters, resolvers y
  acciones al contenedor y cae en la construcción directa para los que este no conoce, así que un
  resolver sin dependencias no necesita registrarse.
- La resolución de instancias sale del motor a `IServiceResolver`. El motor sigue siendo el dueño
  de los planes compilados y se comparte entre todos los mappers de una configuración, de modo que
  registrar `IMapper` como scoped no recompila nada.
- `MappingContext` pasa a llevar también el resolutor y el mapper en curso, para que
  `ResolutionContext.Mapper` devuelva el del scope y no uno global.
- `ProjectTo`: `IQueryable.ProjectTo<TDest>(configuracion)` e `IMapper.ProjectTo<TDest>(consulta)`.
  Un compilador de proyección aparte emite `Expression<Func<TSource,TDest>>` que un proveedor LINQ
  sabe traducir: inicialización de miembros, acceso a miembros, condicionales y `Select`, sin
  bloques, sin variables y sin llamadas a esta librería.
- Vive en el core: solo necesita `IQueryable` y `System.Linq.Expressions`, así que el paquete
  `Mapperion.EntityFrameworkCore` que estaba planeado no hace falta.
- Lo que un proveedor no puede ejecutar se reporta en vez de omitirse: type converters, value
  converters, resolvers y los pasos de `BeforeMap`/`AfterMap`. AutoMapper los omite en silencio; se
  prefirió el error porque una proyección que difiere del mismo mapa por `Map` es un fallo caro de
  encontrar.
- Un mapa que se referencia a sí mismo se reporta también: una proyección se expande por completo
  de antemano, así que un ciclo no tiene fin.
- Las proyecciones se cachean por par de tipos, igual que los planes.
- Tests de integración reales con EF Core y SQLite en memoria: verifican que la consulta se traduce
  a SQL, que solo se piden las columnas del destino y que el filtrado y la paginación siguen
  ocurriendo en la base de datos.
- TFMs `netstandard2.1` y `net472` en los dos paquetes, que pasan a publicar seis: `netstandard2.0`,
  `netstandard2.1`, `net472`, `net8.0`, `net9.0` y `net10.0`.
- `Microsoft.NETFramework.ReferenceAssemblies` aporta los ensamblados de referencia de .NET
  Framework, así que compilar `net472` no exige tener instalado el Developer Pack, ni siquiera en
  Linux.
- `Mapperion.Compatibility.Tests`: una porción representativa de la librería ejecutándose sobre
  .NET Framework real (net472 y net48) además de net8.0 y net10.0. El proyecto reduce sus TFMs
  fuera de Windows, donde .NET Framework no se puede ejecutar.
- Diccionarios: `Dictionary<,>`, `IDictionary<,>` e `IReadOnlyDictionary<,>`, convirtiendo tanto las
  claves como los valores. Se comprueban antes que las colecciones, porque un diccionario también
  es una secuencia de `KeyValuePair<,>` y se mapearía mal.
- La validación también los reconoce ahora: antes reportaba un mapa inexistente de
  `KeyValuePair` a `KeyValuePair` para un diccionario que en realidad se mapeaba bien.
- Una proyección rechaza los diccionarios con un mensaje claro: un proveedor de consultas no tiene
  forma de materializar uno.
- `PreCondition(s => ...)`: descarta el miembro antes incluso de leer su origen.
- `Condition` pasa a evaluarse **después** de resolver el valor, como en AutoMapper. Antes se
  comportaba como una precondición, lo que dejaba a las dos indistinguibles; ahora `Condition`
  paga la lectura y `PreCondition` la evita, que es justo la diferencia entre ambas.
- Un fallo en tiempo de mapeo dice ahora qué miembro lo causó, con la ruta completa a través de
  mapas anidados y colecciones: `Batch.Readings[0].Ratio`. La excepción original queda como
  `InnerException`.
- `MappingException.MemberPath` se rellena de verdad; antes existía y nadie la escribía.
- Un problema de configuración descubierto al compilar un mapa anidado en pleno mapeo sigue
  saliendo como `MapperConfigurationException`, no disfrazado de fallo de mapeo.
- El `catch` del plan no lleva filtro de excepción: un filtro compila a un bloque IL de filtro y
  `DynamicMethod` los rechaza en .NET Framework. Lo detectaron los tests de compatibilidad, que
  pasaban en .NET 8, 9 y 10 y fallaban en net472 y net48.

### Changed

- El plan de un par conocido en tiempo de compilación se alcanza por un hueco numerado en un array
  en vez de buscando una clave en un diccionario. El número es un `static readonly` de un tipo
  genérico, que el JIT pliega a una constante, así que no hay clave que construir ni hash que
  calcular: 6,1 ns a 3,2 ns. El diccionario sigue siendo el único sitio donde se crea un plan;
  esto es una caché delante.
- El contexto de una operación que no necesita estado se construye una vez al crear el mapper, no
  en cada llamada.
- Sobre el mapeo a mano: el record por constructor pasa de 5,45x a 3,46x, el destino existente de
  4,52x a 3,27x, el aplanado de 4,27x a 3,40x y el plano de 3,50x a 3,01x. Las mejoras grandes
  están donde el coste fijo pesaba más, que son los mapas con pocos miembros.

### Fixed

- `MemberDescriptor` se compara por tipo declarante, clase y nombre, no por el `MemberInfo` en
  bruto. La reflexión devuelve un `MemberInfo` distinto para la misma propiedad según el tipo por
  el que se llegue a ella, así que una propiedad heredada aparecía como dos miembros distintos y se
  mapeaba dos veces. Solo salía a la luz con herencia, pero el fallo estaba desde el principio.

- `MaxDepth` y `PreserveReferences` **funcionan**. Se configuraban, se guardaban en el modelo y el
  compilador las ignoraba por completo: eran no-ops silenciosos desde que existe la API fluida.
  La consecuencia era peor que una opción muerta, porque un grafo con un ciclo real recurría hasta
  agotar la pila y tumbaba el proceso con una excepción que ni se puede capturar.
- `PreserveReferences` registra el destino justo después de crearlo y antes de mapear ningún
  miembro, que es lo que permite a un ciclo encontrar el camino de vuelta. Una misma instancia de
  origen produce siempre la misma de destino dentro de una operación.
- `MaxDepth` corta la recursión de ese par de tipos y deja el valor por defecto. El contador se
  libera en un `finally`, así que una excepción no lo deja levantado.
- El estado por operación solo se crea si alguna configuración lo pide, y los diccionarios que
  lleva dentro se crean al primer uso: un mapa que no usa ninguna de las dos no paga nada.
- `AssertIsValid()` detecta ahora los ciclos sin protección recorriendo el grafo de mapas, de modo
  que lo que antes mataba el proceso en producción es un error en arranque.
- `PreserveReferences` sobre tipos por valor se reporta: no hay identidad que preservar.
- `AllowNullDestinationValues` **funciona**. Era el último no-op silencioso: se configuraba, llegaba
  al modelo y nadie la leía. Con la opción desactivada, un miembro cuyo origen resuelve a null
  recibe el contenido vacío del tipo destino: cadena vacía, o una instancia nueva si el tipo tiene
  constructor sin parámetros. Los tipos por valor no se tocan y las colecciones siguen respondiendo
  a `AllowNullCollections`, que es la opción que habla de ellas. En una proyección solo se aplica al
  caso de las cadenas: un proveedor de consultas no puede construir un objeto de la nada.
- Las colecciones se reconstruyen siempre, nunca se comparten. Antes, un `List<X>` hacia `List<X>` o
  hacia `IReadOnlyList<X>` pasaba por el atajo de tipos iguales o asignables y el destino se quedaba
  con **la misma lista** que el origen: cambiar una cambiaba la otra, y además `AllowNullCollections`
  quedaba sin efecto en esos pares. Lo destapó un test de la interacción entre las dos opciones.
