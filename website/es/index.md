---
_layout: landing
---

# Mapperion

Un mapeador objeto a objeto para .NET, **con licencia MIT**, con la misma forma que AutoMapper para
que mover una base de código existente sea casi solo cambiar un namespace.

```csharp
var configuration = new MapperConfiguration(cfg =>
    cfg.CreateMap<Order, OrderDto>());

IMapper mapper = configuration.CreateMapper();
OrderDto dto = mapper.Map<Order, OrderDto>(order);
```

```
dotnet add package Mapperion
```

## Por qué no quedarse en AutoMapper 14

AutoMapper pasó a licencia comercial de pago en la v15. La versión 14 sigue siendo MIT —una
concesión MIT no se puede retirar de algo ya publicado—, pero esa línea está congelada: sin
funcionalidades nuevas, sin nuevos target frameworks y sin arreglos de seguridad.

Lo último no es hipotético. AutoMapper 14 arrastra
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x): un grafo de objetos que se
cierra sobre sí mismo recurre hasta agotar la pila, y no se va a parchear en la línea MIT.
Mapperion pone un techo a la recursión por defecto, así que el mismo grafo lanza una
`RecursionLimitException` que puedes capturar en vez de una `StackOverflowException` que no.

Y el traslado sale barato de una forma comprobada, no prometida. Un ejemplo en el repositorio
configura una misma capa de facturación dos veces, en AutoMapper 14 y en Mapperion, y tumba el
build si las dos discrepan en cualquier campo. El diff entero entre ambas es un `using` por
archivo y un `!`.

## Dos motores, un solo conjunto de reglas

El **motor de runtime** compila un plan la primera vez que se mapea un par y lo reutiliza. Es el
que se comporta como AutoMapper, y es lo que te da `MapperConfiguration`.

El **source generator** escribe ese mismo mapeo como C# corriente en tiempo de compilación. Sin
reflexión y sin emitir código en ejecución, que es lo que lo hace válido bajo trimming y
compilación ahead-of-time — y corre aproximadamente a la velocidad de un mapeo escrito a mano.

Los dos no comparten ni una línea de código. Comparten las reglas, y una suite de pruebas
comprueba que coinciden sobre los mismos casos.

## No viene con nada

`Mapperion` no tiene ninguna dependencia —solo la biblioteca base— en cada uno de sus seis target
frameworks, desde .NET Framework 4.7.2 hasta .NET 10. `ProjectTo` va dentro: construye un árbol de
expresión y deja el resto al proveedor de consultas que tengas, así que no hay que elegir entre un
paquete por ORM.

El source generator y el analizador son paquetes aparte porque ninguno de los dos lleva código que
se ejecute en tu aplicación, y dejarlos fuera es lo que permite que la frase de arriba siga siendo
cierta.

## Por dónde empezar

- [Primeros pasos](articles/getting-started.md) — el primer mapa y la forma de una configuración.
- [Migrar desde AutoMapper](articles/migrating-from-automapper.md) — qué es idéntico, qué cambia a
  propósito y por qué.
- [Ahead-of-time y trimming](articles/aot.md) — el source generator, y lo que el motor de runtime
  no puede hacer.
- [Rendimiento](articles/performance.md) — mediciones contra AutoMapper, Mapster y Mapperly,
  incluido dónde pierde Mapperion.
- [El analizador de configuración](articles/analyzer.md) — un paquete opcional que lee tu
  configuración mientras compila y te dice ahí lo que está mal.
- [Referencia de API](../api/Mapperion.yml) — cada tipo público, generada desde la documentación
  del código. Solo en inglés: sale de los XML docs, y traducirla serían dos verdades sobre la
  misma firma.
