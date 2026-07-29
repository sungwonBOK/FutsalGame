# Tackle Defense Design

Q defense consumes on the first valid attack. Punches are single contact attempts, while a slide tackle repeats overlap checks during its active duration. A blocked tackle must therefore be recorded as a resolved contact for that tackle, otherwise the next physics tick treats the defender as unprotected.

CombatController will distinguish applied, blocked, and evaded hit results. Applied and blocked tackle results will be recorded for the current slide; evaded results will remain eligible for later checks. DefenseController will receive tackle attempts through a distinct method. It will retain the current directional block animation as a fallback until dedicated tackle-block animations and response behavior are added.
