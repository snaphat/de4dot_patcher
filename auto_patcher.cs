using System;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Text;

[assembly:AssemblyVersionAttribute("0.0.2")]

class Program
{
    static StringBuilder outBuffer = new StringBuilder();

    public static bool IsDelegate(dynamic typeDefinition)
    {
        if (typeDefinition.BaseType == null)
            return false;
        return typeDefinition.BaseType.FullName == "System.MulticastDelegate";
    }

    /// <summary>
    /// Prints line separates to the console if verbose mode is enabled.
    /// </summary>
    public static void PrintSeparater()
    {
        Console.WriteLine("============================================================");
    }

    /// <summary>
    /// Checks if a file is in the path.
    /// </summary>
    /// <param name="filename">The file to check existence for.</param>
    /// <returns>Return true if the file is found or false otherwise.</returns>
    static bool CheckFileInPath(String filename)
    {
        //check if the file exists.
        var values = Environment.GetEnvironmentVariable("PATH");
        foreach (var path in values.Split(Path.PathSeparator))
            if (File.Exists(Path.Combine(path, filename)))
                return true; //success (in path)

        //write error.
        outBuffer.Append("Program '" + filename + "' does not exist.");
        return false;
    }

    /// <summary>
    /// Runs a program with the specied arguments.
    /// </summary>
    /// <param name="filename">The program to run.</param>
    /// <param name="arguments">The arguments to the program/</param>
    /// <returns>Returns the programs return code on success and -1 on failure.</returns>
    public static int RunProgram(String filename, String arguments)
    {
        try
        {
            //check if file exists.
            if (CheckFileInPath(filename) == true) {
                //print out what we are doing.
                outBuffer.Append("Running program '" + filename + " " + arguments + "'...\r\n");

                //create a new process instance.
                Process process = new Process();

                //redirect stdout/stderr so that we can print it how we want or not at all.
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                //disable so that the process we are calling uses this programs shell.
                process.StartInfo.UseShellExecute = false;

                //set filename and args
                process.StartInfo.FileName = filename;
                process.StartInfo.Arguments = arguments;

                //run the process
                process.Start();

                //read all of the data from the process (blocks until the end)
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                //wait for exit just in case.
                process.WaitForExit();

                //print the processes messages.
                if (output.Length > 0)
                    outBuffer.Append(output);

                if (error.Length > 0)
                    outBuffer.Append(error);

                //return the processes exit code.
                return process.ExitCode;
            }

            //failed to the find the binary.
            return -1;

        }
        catch (Exception e)
        {
            //some sort of exception occurred.
            outBuffer.Append(e.ToString());
            return -1;
        }
    }

    /// <summary>
    /// Patches the classes and methods of the specified .net assembly using the dnlib assembly provided.
    /// </summary>
    /// <param name="filename">The assembly to patch.</param>
    /// <param name="dnlib">The dnlib assembly.</param>
    /// <returns>Returns zero on success and -1 if some failure occurs.</returns>
    public static int DoPatch(String filename, String dnlib)
    {
        try
        {
            //load the main assembly as bytes.
            byte[] dataFile = System.IO.File.ReadAllBytes(filename);

            //load the dnlib assembly so it can patch things.
            Assembly assembly = Assembly.LoadFrom(dnlib);

            //Grab the dnlib.DotNet.ModuleDefMD type.
            Type moduleDefinitionType = assembly.GetType("dnlib.DotNet.ModuleDefMD");
            Type ModuleCreationOptionsType = assembly.GetType("dnlib.DotNet.ModuleCreationOptions");

            //Get the dnlib.DotNet.ModuleDefMD.Load(byte[], ModuleCreationOptions) method.
            Type[] argumentTypes = { typeof(byte[]), ModuleCreationOptionsType };
            MethodInfo methodInfo = moduleDefinitionType.GetMethod("Load", argumentTypes);

            //Call the dnlib.DotNet.ModuleDefMD.Load(byte[], ModuleCreationOptions) method. Note: it is static, first argument is null as a result.
            Object[] arguments = { dataFile, null };
            dynamic module = methodInfo.Invoke(null, arguments);

            //Loop through each class.
            dynamic classAttributeType = assembly.GetType("dnlib.DotNet.TypeAttributes");
            foreach (dynamic type in module.GetTypes()) {
                //Make each class public.
                if (type.IsClass == true) {
                    //Check if the class is a delegate. If it is NOT, then change its attributes.
                    if (IsDelegate(type) == false) {
                        //Check if the class is nested, then assign the appropriate public attributes.
                        if (type.IsNested == true) {
                            // Nested class: mark as NestedPublic.
                            type.Attributes |= Convert.ChangeType(Enum.Parse(classAttributeType, "NestedPublic"), classAttributeType);
                            //type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "NestedPrivate"), classAttributeType); // Crashes de4dot.
                        }
                        else {
                            // Top level class: mark as Public.
                            type.Attributes |= Convert.ChangeType(Enum.Parse(classAttributeType, "Public"), classAttributeType);
                            type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "NotPublic"), classAttributeType);
                        }

                        // Check if the class is abstract. If it is NOT, then unseal it.
                        if(type.IsAbstract == false) {
                            type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "Sealed"), classAttributeType);
                        }
                    }

                    //Loop through each field.
                    dynamic fieldAttributeType = assembly.GetType("dnlib.DotNet.FieldAttributes");
                    foreach (dynamic field in type.Fields) {
                        //Make each field public.
                        field.Attributes |= Convert.ChangeType(Enum.Parse(fieldAttributeType, "Public"), fieldAttributeType);
                        field.Attributes &= ~Convert.ChangeType(Enum.Parse(fieldAttributeType, "Private"), fieldAttributeType);
                    }

                    //Loop through each method.
                    dynamic methodAttributeType = assembly.GetType("dnlib.DotNet.MethodAttributes");
                    foreach (dynamic method in type.Methods) {
                        //Make each method public.
                        method.Attributes |= Convert.ChangeType(Enum.Parse(methodAttributeType, "Public"), methodAttributeType);
                        method.Attributes &= ~Convert.ChangeType(Enum.Parse(methodAttributeType, "Private"), methodAttributeType);

                        //Make each method virtual if it isn't static/constructor/abstract
                        if(method.IsStatic == false  && method.IsAbstract == false && method.IsConstructor == false) {
                            //make each method virtual.
                            method.Attributes |= Convert.ChangeType(Enum.Parse(methodAttributeType, "Virtual"), methodAttributeType);
                        }
                    }
                }
            }

            //Write out the module.
            module.Write(filename);

            return 0;
        }
        catch (Exception e)
        {
            //some sort of exception occurred.
            outBuffer.Append(e.ToString());
            return -1;
        }
    }

    static int MergeAndPatch(String origFile, String modFile)
    {
        //check if file exists.
        if (!File.Exists(origFile)) {
            return 0;
        }

        //Merge de4dot with all of the assemblies it depends on into an assembly called de4dotp.exe.
        int returnValue;
        PrintSeparater();
        Console.WriteLine("Merging '"+ origFile +"' and DLLs into a packed executable '"+ modFile +"'...\r\n");
        String ilmergeProgram = "ILRepack.exe";
        String arguments = ".\\"+origFile + " AssemblyData.dll AssemblyServer.exe de4dot.blocks.dll de4dot.code.dll de4dot.cui.dll de4dot.mdecrypt.dll dnlib.dll /out:" + modFile;
        returnValue = RunProgram(ilmergeProgram, arguments);

        if (returnValue == 0) {
            outBuffer.Clear(); //clear the buffer since it adds any output messages.
            PrintSeparater();

            //Patch de4dotp.exe to allow public access to all classes, fields, and methods and virtualizing methods.
            Console.WriteLine("Patching '"+ modFile +"', (1) Publicizing classes/fields/methods, (2) unsealing classes, (3) virtualizing methods...\r\n");
            returnValue = DoPatch(".\\"+modFile, "dnlib.dll");
        }

        //print error message
        if (returnValue != 0) {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(outBuffer);
        }

        //print success
        PrintSeparater();
        if (returnValue == 0)
            Console.WriteLine(origFile + " packed and patched successfully!\r\n");

        //remove a generated pdb from ilmerge if it exists.
        File.Delete(modFile.Split('.')[0]+".pdb");
        File.Delete(modFile+".config");
        return returnValue;
    }

    static int Main(string[] args)
    {
        int ret = MergeAndPatch("de4dot.exe", "de4dotp.exe");
        if (ret == 0) { //if the x86 version was merged successfully (or doesn't exist), then merge the x64 version.
            ret = MergeAndPatch("de4dot-x64.exe", "de4dotp-x64.exe");
        }

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
        return ret;
    }
}
