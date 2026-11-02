# Versionsregler / Version policy

## Dansk

Den første udviklingsversion er **0.0.001**. `VERSION` og `HubVersion.Display` indeholder det brugervendte nummer. MSBuild/NuGet bruger normaliseret `0.0.1`; .NET assembly- og Stream Deck-manifestversioner bruger `0.0.1.0`.

Alle versioner er udviklingsversioner, indtil ejeren har godkendt andet. **Ingen release eller tag uden udtrykkelig godkendelse.** Hver passage af en .100-grænse — eksempelvis 0.0.099 → 0.0.100 og 0.0.199 → 0.0.200 — kræver særskilt godkendelse, også uden en release. Nummeret er et aftalt fremdriftssignal og må ikke automatisk øges.

**0.1.000** kræver, at det aftalte scope er fuldført, at test i det aktive miljø er dokumenteret og accepteret, og at ejeren udtrykkeligt godkender skiftet. Scope og acceptkriterier skal afklares, ikke antages.

Der findes ingen release-, tag- eller automatisk versionsworkflow. CI kontrollerer aktuelt 0.0.001. En godkendt senere versionsændring skal opdatere versionsfelter, test, dokumentation og ændringslog samlet i en PR. Procesreglerne er ikke en GitHub-administratorlås; branch protection og ejerreview skal konfigureres særskilt, hvis håndhævelse ønskes.

## English

The first development version is **0.0.001**. `VERSION` and `HubVersion.Display` contain the user-facing number. MSBuild/NuGet use normalized `0.0.1`; .NET assembly and Stream Deck manifest versions use `0.0.1.0`.

All versions remain development versions until the owner approves otherwise. **No release or tag without explicit approval.** Every .100 boundary — for example 0.0.099 → 0.0.100 and 0.0.199 → 0.0.200 — requires separate approval, even without a release. The number is an agreed progress signal and must not increment automatically.

**0.1.000** requires completion of the agreed scope, documented and accepted testing in the active environment, and explicit owner approval of the transition. Scope and acceptance criteria must be established rather than assumed.

There is no release, tag or automatic versioning workflow. CI currently checks 0.0.001. An approved future version change must update version fields, tests, documentation and changelog together in a PR. These process rules are not a GitHub administrator lock; branch protection and owner review must be configured separately if enforcement is required.
