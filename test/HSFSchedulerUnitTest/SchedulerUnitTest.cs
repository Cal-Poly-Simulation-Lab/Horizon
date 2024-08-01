using Microsoft.VisualStudio.TestPlatform.TestHost;
using Horizon;
using Utilities;
using NUnit.Framework.Internal;

namespace HSFSchedulerUnitTest;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    
  
    }

    [Test]
    public void Test1()
    {
        // Declare all files used for this test
        string SimInputFile   = "SchedulerTestSimulationInput.json";
        string TaskInputFile  = "SchedulerTestTasks.json";
        string ModelInputFile = "SchedulerTestModel.json";

        //Set up the StringWrite so we can see what the Horizon Program is doing from a Console.WriteLine() POV:
        StringWriter stringWriter = new StringWriter(); 
        Console.SetOut(stringWriter); 

        // Load all files and create a new Horizon Program
        SchedulerTestHelper(SimInputFile,TaskInputFile,ModelInputFile);

        //
    }

    public void SchedulerTestHelper(string SimInputFile, string TaskInputFile, string ModelInputFile)
    {
            #region Input File (argsList) Pathing Setup & Validation

            // Get the test directory in the Horizon repo
            string TestDirectory = Utilities.DevEnvironment.GetTestDirectory(); 
            // Set default directory to the HSFSchedulerUnitTest
            string SchedulerTestDirectory = Path.Combine(TestDirectory,"HSFSchedulerUnitTest");
            
            // Check if the input files exist (full path was passed) if not, assume relative path from SchedulerTestDirectory
            if (!File.Exists(SimInputFile))   { SimInputFile   = Path.Combine(SchedulerTestDirectory,SimInputFile);   }
            if (!File.Exists(TaskInputFile))  { TaskInputFile  = Path.Combine(SchedulerTestDirectory,TaskInputFile);  }
            if (!File.Exists(ModelInputFile)) { ModelInputFile = Path.Combine(SchedulerTestDirectory,ModelInputFile); }
            

            // Initiate a (spoofed) argsList as if input from the CLI to the console application:
            List<string> argsList = new List<String>(); 

            // Check if the input files above exist before adding them to the argsList: 
            if (File.Exists(SimInputFile)) { argsList.Add("-s"); argsList.Add(SimInputFile); } 
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Simulation Input file was found. Using default.");}
            if (File.Exists(TaskInputFile)) { argsList.Add("-t"); argsList.Add(TaskInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Task Input file was found. Using default.");}
            if (File.Exists(ModelInputFile)) { argsList.Add("-m"); argsList.Add(ModelInputFile); }
            else { Console.WriteLine("HSFSchedulerUnitTest: No valid Test Model Input file was found. Using default.");}
            
            // Check and create the test output directory. 
            string outputDir = Path.Combine(SchedulerTestDirectory,@"output/");
            if (!Directory.Exists(outputDir)) { Directory.CreateDirectory(outputDir); }
            // Add the output directory to the argsList
            argsList.Add("-o"); argsList.Add(outputDir);

            #endregion

            // Create a new Horizon program
            Horizon.Program program = new Horizon.Program(); 

            // Run Horizon like normal to load all necessary elements: 
            program.InitInput(argsList);
            program.InitOutput(argsList);
            program.LoadScenario();
            program.LoadTasks();
            program.LoadSubsystems();
            program.LoadEvaluator();

            // Now it is time to test the scheduler: 
            program.CreateSchedules();
            //double maxSched = program.EvaluateSchedules();

    }



}
