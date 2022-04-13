﻿using System;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Text;

class Program
{
    static StringBuilder errorBuffer = new StringBuilder();
    
    public static bool IsDelegate(dynamic typeDefinition)
    {
        if (typeDefinition.BaseType == null)
        {
            return false;
        }
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
    /// Gets the program files (x86) directory on all windows versions.
    /// </summary>
    /// <returns>returns the program files directory</returns>
    static string GetProgramFilesx86()
    {
        if (8 == IntPtr.Size
            || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
        {
            return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        }

        return Environment.GetEnvironmentVariable("ProgramFiles");
    }

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    /// <param name="filename">The file to check existence for.</param>
    /// <returns>Return true if the file is found or false otherwise.</returns>
    static bool CheckFileExists(String filename)
    {
        //check if the file exists.
        if (File.Exists(filename) == false)
        {
            //write error.
            errorBuffer.Append("Program '" + filename + "' does not exist.");

            //failure
            return false;
        }
        else
        {
            //success
            return true;
        }
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
            if (CheckFileExists(filename) == true)
            {
                //print out what we are doing.
                errorBuffer.Append("Running program '" + filename + " " + arguments + "'...\r\n");

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
                {
                    errorBuffer.Append(output);
                }

                if (error.Length > 0)
                {
                    errorBuffer.Append(error);
                }

                //return the processes exit code.
                return process.ExitCode;
            }

            //failed to the find the binary.
            return -1;

        }
        catch (Exception e)
        {
            //some sort of exception occurred.
            errorBuffer.Append(e.ToString());
            return -1;
        }
    }

    /// <summary>
    /// Patches the classes and methods of the specified .net assembly using the Mono.Cecil assembly provided.
    /// </summary>
    /// <param name="filename">The assembly to patch.</param>
    /// <param name="monoCecil">The Mono.Cecil assembly.</param>
    /// <param name="bBackup">Whether to backup the file or not before patching.</param>
    /// <returns>Returns zero on success and -1 if some failure occurs.</returns>
    public static int DoPatch(String filename, String dnlib, bool bBackup)
    {
        try
        {
            //check if file exists.
            if (CheckFileExists(filename) == true)
            {
                //check whether to backup file
                if (bBackup == true)
                {
                    //Moving file
                    String backupFile = filename + ".bak";
                    File.Copy(filename, backupFile);
                }
                
                byte[] dataFile = System.IO.File.ReadAllBytes(filename);

                //load the dnlib assembly.
                Assembly assembly = Assembly.LoadFrom(dnlib);

                //Grab the dnlib.DotNet.ModuleDefMD type.
                Type moduleDefinitionType = assembly.GetType("dnlib.DotNet.ModuleDefMD");

                //Get the dnlib.DotNet.ModuleDefMD.Load(byte[]) method.
                Type[] argumentTypes = { typeof(byte[]) };
                MethodInfo methodInfo = moduleDefinitionType.GetMethod("Load", argumentTypes);

                //Call the dnlib.DotNet.ModuleDefMD.Load(byte[]) method. Note: it is static, first argument is null as a result.
                Object[] arguments = { dataFile };
                dynamic module = methodInfo.Invoke(null, arguments);

                //Loop through each class.
                dynamic classAttributeType = assembly.GetType("dnlib.DotNet.TypeAttributes");
                foreach (dynamic type in module.GetTypes())
                {
                    //Make each class public.
                    if (type.IsClass == true)
                    {
                        //Check if the class is a delegate. If it is, then don't change its attributes.
                        if (IsDelegate(type) == false)
                        {
                            //check whether class is nested then assign the appopriate public.
                            if (type.IsNested == true)
                            {
                                type.Attributes |= Convert.ChangeType(Enum.Parse(classAttributeType, "NestedPublic"), classAttributeType);
                                //type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "NestedPrivate"), classAttributeType);
                            }
                            else
                            {
                                type.Attributes |= Convert.ChangeType(Enum.Parse(classAttributeType, "Public"), classAttributeType);
                                type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "NotPublic"), classAttributeType);
                            }
                            
                            //unseal the class.
                            if(type.IsAbstract == false)
                            {
                                type.Attributes &= ~Convert.ChangeType(Enum.Parse(classAttributeType, "Sealed"), classAttributeType);
                            }
                        }
                        
                        //Loop through each field.
                        dynamic fieldAttributeType = assembly.GetType("dnlib.DotNet.FieldAttributes");
                        foreach (dynamic field in type.Fields)
                        {
                            //Make each field public. 
                            field.Attributes |= Convert.ChangeType(Enum.Parse(fieldAttributeType, "Public"), fieldAttributeType);
                            field.Attributes &= ~Convert.ChangeType(Enum.Parse(fieldAttributeType, "Private"), fieldAttributeType);
                        }

                        //Loop through each method.
                        dynamic methodAttributeType = assembly.GetType("dnlib.DotNet.MethodAttributes");
                        foreach (dynamic method in type.Methods)
                        {
                            //Make each method public.
                            method.Attributes |= Convert.ChangeType(Enum.Parse(methodAttributeType, "Public"), methodAttributeType);
                            method.Attributes &= ~Convert.ChangeType(Enum.Parse(methodAttributeType, "Private"), methodAttributeType);
                            
                            //Make each method virtual if it isn't static/constructor/abstract
                            if(method.IsStatic == false  && method.IsAbstract == false && method.IsConstructor == false)
                            {
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
        }
        catch (Exception e)
        {
            //some sort of exception occurred.
            errorBuffer.Append(e.ToString());
            return -1;
        }

        return -1;
    }

    static int Main(string[] args)
    {
        string origFile = "de4dot.exe";
        string modFile = "de4dotp.exe";
        //return value
        int returnValue;

        //separater
        PrintSeparater();

        //Merge de4dot with all of the assemblies it depends on into an assembly called de4dotp.exe.
        Console.WriteLine("Merging '"+ origFile +"' and DLLs into a packed executable '"+ modFile +"'...\r\n");
        String ilmergeProgram = GetProgramFilesx86() + "\\Microsoft\\ILMerge\\ILMerge.exe";
        String arguments = origFile + " bin\\AssemblyData.dll bin\\AssemblyServer.exe bin\\de4dot.blocks.dll bin\\de4dot.code.dll bin\\de4dot.cui.dll bin\\de4dot.mdecrypt.dll bin\\dnlib.dll /out:" + modFile;
        returnValue = RunProgram(ilmergeProgram, arguments);

        if (returnValue == 0)
        {
            //clear the error buffer since it adds any output messages.
            errorBuffer.Clear();

            //separater
            PrintSeparater();

            //Patch de4dotp.exe to allow public access to all classes, fields, and methods and virtualizing methods.
            Console.WriteLine("Patching '"+ modFile +"', (1) Publicizing classes/fields/methods, (2) unsealing classes, (3) virtualizing methods...\r\n");
            returnValue = DoPatch(modFile, "bin\\dnlib.dll", false);
        }

        //print error message
        if (returnValue != 0)
        {
            Console.WriteLine("An error occurred:");
            Console.WriteLine(errorBuffer);
        }

        //separater
        PrintSeparater();

        //print success
        if (returnValue == 0)
        {
            Console.WriteLine("de4dotp.exe packed and patched successfully!\r\n");
        }

        //remove a generated pdb from ilmerge if it exists.
        File.Delete("de4dotp.pdb");

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
        return returnValue;
    }
}
