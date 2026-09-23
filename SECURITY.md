# Security

## Reporting something

Use GitHub's private vulnerability reporting: **Security → Report a vulnerability** on
<https://github.com/Veneury/Mapperion>. That opens a thread only the maintainers can see, so
nothing is public while a fix is being worked out.

Please do not open a normal issue for a vulnerability. A public issue tells everyone at once,
including the people you would rather it did not.

What helps in a report: what an attacker can do, the configuration and input that gets them there,
and which version you saw it on. A failing test is worth more than a description.

You should get an acknowledgement within a week. If a week passes with nothing, assume the message
did not arrive and say so in a public issue without describing the problem.

## What counts

A mapper sits between data a program did not create and objects it trusts, so the interesting
cases are about what happens when the input is hostile rather than merely wrong:

- Input that makes mapping fail to terminate, exhaust memory, or take the process down. The
  recursion ceiling exists for exactly this and a way past it is a vulnerability.
- Configuration or input that causes a type to be loaded, constructed or invoked that the
  configuration never named.
- Anything that lets one mapping operation see values belonging to another.

Not vulnerabilities, though still worth an ordinary issue: a mapping that produces the wrong value,
a configuration mistake reported unhelpfully, or a slow map.

## Supported versions

Before 1.0, only the most recent version is supported. A fix goes out as a new version rather than
being backported.

After 1.0 this section will say which majors receive fixes and for how long.

## What is already known

Mapperion bounds recursion by default, so an object graph that loops raises
`RecursionLimitException` rather than exhausting the stack. This is deliberate and is the shape of
[CVE-2026-32933](https://github.com/advisories/GHSA-rvv3-g6hj-g44x) in AutoMapper, which is not
fixed on its MIT line. If you find a graph that gets past the ceiling, that is worth reporting
through the process above.
