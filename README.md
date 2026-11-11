# Horizon Simulation Framework (HSF)
![Horizon Simulation Framework banner](assets/hsf-banner.png)

HSF is a simulation framework for exploring feasible task schedules for complex systems. It combines dynamic programming based optimization algorithms with a hybrid, discrete-event simulation and a user-defined system model. HSF combines an optimal scheduling algorithm based on user defined requirements and system constraints, and a dynamic system theory modeling paradigm to produce simulations of complex systems in operation.  HSF evaluates proposed tasks, checks system constraints, and tracks changes in system state over time.

## Why HSF?

<p align="center">
  <img src="assets/hsf-logo.png" align="right" alt="HSF logo" width="180">
</p>

System-level requirements describe how a system must perform in use. Those requirements can be difficult to verify before the full system is operational. HSF lets a user model system behavior and evaluate candidate schedules against that model.

The framework separates schedule generation from system behavior. The scheduler proposes tasks and candidate schedules. A modular system model determines whether each task can be performed and how it changes the system state.  All elements of HSF, like tasks, schedules, and systems, are user defined to maximize flexibility and application.

## How it works

![How HSF works](/assets/Infographic%20with%20rounded%20transparent%20corners.png)

At a high level, HSF:

1. Generates candidate schedules by proposing tasks within their available access windows.  An access window is the time and place a task can be preformed by the system.
2. Evaluates each proposed task against the system model and its constraints.  Constraints are both imposed requierments and physical limitations.
3. Retains feasible schedules and scores them against user-defined objectives or requirements.  The output supports mission design and planning.

Core concepts include:

- **Asset:** the core system modeling element that simulates the motion of the system and how all states of the system change in time and in response to a proposed task.
- **Subsystem:** the modeling element that defines system capabilities and state changes.  Collections of **subsystems** form an **asset**.
- **Task:** an action for an asset to perform at a target.
- **Event:** a task together with the system state at a simulation time step.
- **Schedule:** a time-ordered collection of events including full system state data and asset information.

- **Constraint:** a condition that a subsystem or system state must satisfy - allows the user to impose system and subsystem requierments.
- **Dependency:** a defined interface through which subsystems exchange information.

## Visualization with Cesium ion

![HSF in Cesium Ion](assets/cesium-example.png)

A Cesium ion-based visualization effort is underway as an important companion to HSF. The visualization project is intended to make mission scenarios and simulation results easier to explore in an interactive geospatial view. For updates on the project, check out the GitHub [HSF Cesium Viewer](https://github.com/Cal-Poly-Simulation-Lab/cesium-viewer).

## Example: Aeolus mission

Aeolus is the Greek God of the winds.  The mission of the Aeouls Space System (as proposed) is to study major wind driven events in the lower Earth atmosphere from space.  The HSF Aeolus project is an example of how HSF can be used to simulate a complex system in operation. The example models a constellation of two spacecraft in orbit with simple Earth Observing capability for two orbits.  HSF runs the simulation and reports target-imaging and asset-state outputs for all mission time for overall **utilitiy analysis** and helps engineers answer the question, "does the system as designed meet the system level requierments?  And can the subsystems meet the requierments of the proposed mission profile?"

Example outputs from HSF Aeolus shown below include target hits, sensor on/off state, solar-panel power, battery depth of discharge, data rate, buffer fill ratio, and image pixel count.

ADD AEOLUS PLOTS

## Tutorials

The HSF Aeolus Example is great for mid-level users.  But we created an example for new users that model a single spacecraft in orbit taking images of Earth.  In this example, HSF tracks various constraints and state data of the systems.  Your first HSF project is here (ADD LINK)

## Example applications

Over the years, the HSF modeling and simulation paradighm has been applied to a number of cases.  These projects have been described in a handfule of MS theses by Cal Poly Students.  (ADD LINKS)

- Thermal soaring
- CubeSats
- Sounding rockets
- UAV swarm search and rescue
- A 6U space telescope constellation
- Lunar Gateway trajectory analysis

## Getting started

> **Draft note:** Confirm the current supported language, dependencies, installation steps, and a runnable example before publishing this section.

1. Install the project dependencies: **[add instructions]**
2. Run the example: **[add command or notebook link]**
3. Review the generated schedules and simulation outputs: **[add output location or viewer instructions]**

## Documentation

- **Examples** [Add link]
- **Developer Documentation** [Add link]
- [Academic/Thesis documentation](https://github.com/emehiel/Horizon/wiki/Academic-Documentation)
- [Cesium ion visualization documentation](https://github.com/Cal-Poly-Simulation-Lab/cesium-viewer)

## Project status

**Status:** **[confirm current development status and supported release]**

The material used to prepare this draft describes the framework and example scenarios but does not specify a current release, installation workflow, license, or contribution process. Add those project details before treating this README as final.

## Contributing

Contribution guidelines: **[add link or instructions]**

## License

License: **[add license name and link]**

## Citation

If you use HSF in academic work, cite: **[add preferred citation]**
