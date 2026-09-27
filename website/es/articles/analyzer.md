# El analizador de configuración

`Mapperion.Analyzers` es un paquete aparte y opcional. Lee tu configuración de mapeo mientras el
proyecto compila y reporta los errores que se ven en lo que escribiste.

```bash
dotnet add package Mapperion.Analyzers
```

Está aparte para que `Mapperion` siga sin tener ninguna dependencia. No cambia nada más: el
analizador no lleva código de ejecución y nada suyo llega a tu salida.

## Qué reporta

Todo es un aviso, nunca un error. Dos de los cuatro describen algo que lanza al construir la
configuración, así que un error sería defendible, pero esos mismos dos se pueden escribir en ramas
de un `if` donde solo una llega a ejecutarse. Un proyecto que quiera que esto tumbe el build ya
convierte los avisos en errores.

### MPR1001 — el par ya está declarado

```csharp
cfg.CreateMap<Person, PersonDto>();
cfg.CreateMap<Person, PersonDto>();   // MPR1001
```

Un par se declara una sola vez por configuración. El segundo lanza
`MapperConfigurationException` al construirla, así que esto solo adelanta la queja — y adelantada
es donde sale barata.

### MPR1002 — al miembro se le da origen más de una vez

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Nickname));   // MPR1002
```

`MapFrom` reemplaza el origen que tuviera el miembro, así que el primero simplemente ya no está
aunque se siga leyendo como si aplicara.

Esto es sobre el origen y nada más. Los ajustes se acumulan, así que repartir la configuración de
un miembro en dos llamadas es un estilo y no un error:

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => o.MapFrom(s => s.Name))
   .ForMember(d => d.Name, o => o.SetMappingOrder(5));         // no se reporta nada
```

### MPR1003 — el miembro se ignora y se le da origen

```csharp
cfg.CreateMap<Person, PersonDto>()
   .ForMember(d => d.Name, o => { o.MapFrom(s => s.Name); o.Ignore(); });   // MPR1003
```

`Ignore` quita el origen y `MapFrom` quita el ignore, así que uno de los dos no está haciendo nada
y cuál depende del orden. Se lee igual escrito en dos llamadas, donde cuesta más verlo, y ahí
también se reporta.

### MPR1004 — el destino se construye de dos maneras

```csharp
cfg.CreateMap<Person, PersonRecord>()
   .ConstructUsing(s => new PersonRecord(s.Name, s.Age))
   .ForCtorParam("name", o => o.MapFrom(s => s.Name));   // MPR1004
```

`ConstructUsing` entrega la construcción entera a una fábrica, así que no quedan argumentos de
constructor que `ForCtorParam` pueda configurar.

## Qué no hace

**No te dice si un miembro de destino va a encontrar origen.** Para eso está `AssertIsValid()`.
Responderlo en tiempo de compilación significaría una segunda copia del motor de convenciones
viviendo al lado de la primera y separándose de ella, y una respuesta equivocada de un analizador
es peor que ninguna respuesta.

**Sigue cadenas fluidas.** Una configuración que guarda la expresión en una variable y la llama
más tarde no se sigue, y no se reporta nada en vez de reportar algo equivocado:

```csharp
var map = cfg.CreateMap<Person, PersonDto>();
map.ForMember(d => d.Name, o => o.MapFrom(s => s.Name));
map.ForMember(d => d.Name, o => o.MapFrom(s => s.Nickname));   // no se reporta
```

**Distingue tu builder del nuestro.** Todo se empareja por símbolo, así que un `ForMember` de la
API fluida de otro queda en paz.

## Apagar uno

Como con cualquier analizador, en `.editorconfig`:

```ini
dotnet_diagnostic.MPR1002.severity = none
```

O en un sitio concreto, donde el error es a propósito — un test que comprueba que la configuración
lo rechaza, por ejemplo:

```csharp
#pragma warning disable MPR1001
// ...
#pragma warning restore MPR1001
```

La propia suite de pruebas de la librería hace justo eso en tres sitios, y corre el analizador
sobre sí misma en todos los demás.
