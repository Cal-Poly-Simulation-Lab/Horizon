# Thesis Notes - Horizon Scheduler System

## 1. All Test Coverage

### Comprehensive Unit Testing Framework
- **GenerateExhaustiveSystemSchedules**: Validates combinatorial generation algorithm (Tasks^Assets)
- **SystemScheduleConstructor**: Tests time boundary assignment logic for events and tasks
- **CanAddTasks**: [To be documented] - Task addition validation logic
- **TimeDeconfliction**: [To be documented] - Schedule conflict resolution
- **CropSchedules**: [To be documented] - Schedule optimization and pruning

**Expanded explanation**: The test suite covers the entire scheduling pipeline from initial combination generation through final schedule construction and validation. Each component is isolated and tested with both normal and edge case scenarios, ensuring robust behavior across the system.

## 2. Specific Decided Usage

### Event-Task Relationship Architecture
- **Events are fundamental timestep window**
- **Tasks can only be within event window**
  - This can be expanded in future
- **Access are either Aeolus pregen or default full access combo gen**
  - Scripted access generation is something that can be implemented but there is no current "Check" for whether or not it would be done correctly
  - Only exception handles in the SystemSchedule constructor

**Expanded explanation**: The current architecture enforces a strict temporal boundary model where tasks must complete within a single fundamental timestep (event window). This design choice simplifies scheduling logic and ensures predictable behavior, but limits task flexibility. The access generation system supports two modes: (1) Aeolus-generated access windows based on orbital mechanics, and (2) default full-simulation access windows for combinatorial testing. Future scripted access generation would require additional validation beyond the current exception handling in the SystemSchedule constructor.

## 3. Other Functionality

### ScriptedSubsystemCS.cs
- Dynamic C# subsystem compilation and loading
- Runtime assembly resolution and type validation
- Integration with JSON-based subsystem configuration
- Error handling for subsystem inheritance validation

**Expanded explanation**: The ScriptedSubsystemCS.cs module enables dynamic loading of user-defined subsystems written in C#. It compiles C# code at runtime, validates that classes properly inherit from the Subsystem base class, and integrates with the JSON configuration system. This provides flexibility for users to define custom subsystem behaviors without requiring recompilation of the entire Horizon system. The module includes robust error handling for common issues like missing dependencies, inheritance violations, and compilation errors.

---

## Future Additions
*Additional functionality and architectural decisions to be documented as development progresses*
