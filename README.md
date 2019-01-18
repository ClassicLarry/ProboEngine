# ProboEngine

The attached version of ProboEngine is a Visual Studio C# Windows Desktop Console App (.Net Framework) meant to be ran locally. An exported version of the complete project is attached and all of the individual classes have been uploaded as well. It is necessary to download math.numerics from a nuget package manager in order to run the matrix computations. The project files are structured in the following manner.

"Program" is the initial class where the user input (requested units and upgrades) are specified.
 
Within this class, "IterationManager" performs the majority of the computations. First a new "buildCreatorPreEcon" class is initialized. This class is where all of the unit, building, and upgrade game data is specified.
 
Next, "IterationManager" iterates through attempts to estimate the economic and non-economic portions of the build order until convergence is achieved. The economic portion is estimated with the function "optimizeEconomicVariables" and the non-economic portion is estimated with the function "createCompressedBuilds" under "buildCreatorPreEcon".
