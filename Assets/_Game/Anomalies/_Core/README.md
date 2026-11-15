# Shared anomaly lifecycle

`FloorLoopManager` owns floors, stairs and transitions. `FloorAnomalyController` selects one implemented effect and resets all effects between floors. `FloorAnomaly` owns each effect lifecycle. `AnomalyDefinition` stores design numbers and notes.

Effects must restore every modified object/material/light, stop coroutines and audio, and tolerate repeated resets. Store mutable state on the component, not in static fields or shared material assets. Unimplemented definitions never participate in selection.
