# Shared anomaly lifecycle

`FloorLoopManager` owns floors, stairs and transitions. `FloorAnomalyController` selects one implemented effect and resets all effects between floors. `FloorAnomaly` owns each effect lifecycle. `AnomalyDefinition` stores design numbers and notes.

Effects must restore every modified object/material/light, stop coroutines and audio, and tolerate repeated resets. Store mutable state on the component, not in static fields or shared material assets. Unimplemented definitions never participate in selection.

The 7188 admin panel uses the same scene registrations. It enables existing rows for implemented anomalies and creates rows for new numbers when opened. Set the definition's display name and connect the component under GameLoop to make it available in the panel.
