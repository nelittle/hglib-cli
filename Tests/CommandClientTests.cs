using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

using Mercurial;

namespace Mercurial.Tests
{
	[TestFixture()]
	public class CommandClientTests
	{
		static readonly string TestRepo = "http://selenic.com/hg";
		static List<string> garbage = new List<string> ();
		
		
		[SetUp]
		public void Setup ()
		{
		}
		
		[TearDown]
		public void Teardown ()
		{
			foreach (string garbageDir in garbage) {
				try {
					Directory.Delete (garbageDir, true);
				} catch {
					// Don't care
				}
			}
		}
		
		[Test]
		public void TestConnection ()
		{
			using (new CommandClient (null, null, null)) {
			}
		}
		
		[Test]
		public void TestConfiguration ()
		{
			using (CommandClient client = new CommandClient (null, null, null)) {
				IDictionary<string,string > config = client.Configuration;
				Assert.IsNotNull (config);
				Assert.Greater (config.Count, 0, "Expecting nonempty configuration");
				// Console.WriteLine (config.Aggregate (new StringBuilder (), (s,pair) => s.AppendFormat ("{0} = {1}\n", pair.Key, pair.Value), s => s.ToString ()));
			}
		}
		
		[Test]
		public void TestInitialize ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path);
			Assert.That (Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not created at {0}", path));
		}
		
		[Test]
		public void TestRoot ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path);
			Assert.That (Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not created at {0}", path));
			
			using (var client = new CommandClient (path, null, null)) {
				Assert.AreEqual (path, client.Root, "Unexpected repository root");
			}
		}
		
		[Test]
		public void TestCloneRemote ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Clone (TestRepo, path, true, null, "10", null, false, true);
			Assert.That (Directory.Exists (Path.Combine (path, ".hg")), string.Format ("Repository was not cloned from {0} to {1}", TestRepo, path));
		}
		
		[Test]
		public void TestCloneLocal ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath);
			
			using (var client = new CommandClient (firstPath, null, null)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1");
			}
			
			CommandClient.Clone (firstPath, secondPath);
			Assert.That (Directory.Exists (Path.Combine (secondPath, ".hg")), string.Format ("Repository was not cloned from {0} to {1}", firstPath, secondPath));
			Assert.That (File.Exists (Path.Combine (secondPath, "foo")), "foo doesn't exist in cloned working copy");
				
			using (var client = new CommandClient (secondPath, null, null)) {
				IList<Revision> log = client.Log (null);
				Assert.AreEqual (1, log.Count, "Unexpected number of log entries");
			}
		}
		
		[Test]
		public void TestAdd ()
		{
			string path = GetTemporaryPath ();
			IDictionary<string,Status > statuses = null;
			
			CommandClient.Initialize (path);
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (Path.Combine (path, "foo"), string.Empty);
				File.WriteAllText (Path.Combine (path, "bar"), string.Empty);
				client.Add (Path.Combine (path, "foo"), Path.Combine (path, "bar"));
				statuses = client.Status (null);
			}
			
			Assert.IsNotNull (statuses);
			Assert.That (statuses.ContainsKey ("foo"), "No status received for foo");
			Assert.That (statuses.ContainsKey ("bar"), "No status received for bar");
			Assert.AreEqual (Status.Added, statuses ["foo"]);
			Assert.AreEqual (Status.Added, statuses ["bar"]);
		}
		
		[Test]
		public void TestCommit ()
		{
			string path = GetTemporaryPath ();
			CommandClient.Initialize (path);
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (Path.Combine (path, "foo"), string.Empty);
				File.WriteAllText (Path.Combine (path, "bar"), string.Empty);
				client.Add (Path.Combine (path, "foo"));
				client.Commit ("Commit all");
				Assert.That (!client.Status ().ContainsKey ("foo"), "Default commit failed for foo");
				
				client.Add (Path.Combine (path, "bar"));
				client.Commit ("Commit only bar", Path.Combine (path, "bar"));
				Assert.That (!client.Status ().ContainsKey ("bar"), "Commit failed for bar");
				Assert.AreEqual (2, client.Log (null).Count, "Unexpected revision count");
			}
		}
		
		[Test]
		public void TestLog ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path);
			
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1");
				File.WriteAllText (file, "2");
				client.Commit ("2");
				Assert.AreEqual (2, client.Log (null).Count, "Unexpected revision count");
			}
		}
		
		[Test]
		public void TestAnnotate ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			CommandClient.Initialize (path);
			
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (file, "1");
				client.Add (file);
				client.Commit ("1", null, false, false, null, null, null, DateTime.MinValue, "user");
				Assert.AreEqual ("user 0: 1", client.Annotate (null, file));
			}
		}
		
		[Test]
		public void TestDiff ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string diffText = string.Empty;
			CommandClient.Initialize (path);
			
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (file, "1\n");
				client.Add (file);
				client.Commit ("1", null, false, false, null, null, null, DateTime.MinValue, "user");
				File.WriteAllText (file, "2\n");
				diffText = client.Diff (null, file);
			}
			
			string[] lines = diffText.Split (new[]{"\n"}, StringSplitOptions.RemoveEmptyEntries);
			Assert.AreEqual (6, lines.Length, "Unexpected diff length");
			Assert.AreEqual ("@@ -1,1 +1,1 @@", lines [3]);
			Assert.AreEqual ("-1", lines [4]);
			Assert.AreEqual ("+2", lines [5]);
		}
		
		[Test]
		public void TestExport ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			string diffText = string.Empty;
			CommandClient.Initialize (path);
			
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (file, "1\n");
				client.Add (file);
				client.Commit ("1");
				File.WriteAllText (file, "2\n");
				client.Commit ("2");
				diffText = client.Export ("1");
			}
			string[] lines = diffText.Split (new[]{"\n"}, StringSplitOptions.RemoveEmptyEntries);
			Assert.AreEqual (12, lines.Length, "Unexpected diff length");
			Assert.AreEqual ("@@ -1,1 +1,1 @@", lines [9]);
			Assert.AreEqual ("-1", lines [10]);
			Assert.AreEqual ("+2", lines [11]);
		}
		
		[Test]
		public void TestForget ()
		{
			string path = GetTemporaryPath ();
			string file = Path.Combine (path, "foo");
			IDictionary<string,Status > statuses = null;
			
			CommandClient.Initialize (path);
			using (var client = new CommandClient (path, null, null)) {
				File.WriteAllText (file, string.Empty);
				client.Add (file);
				statuses = client.Status (null);
				
				Assert.IsNotNull (statuses);
				Assert.That (statuses.ContainsKey ("foo"), "No status received for foo");
				Assert.AreEqual (Status.Added, statuses ["foo"]);
				
				client.Forget (file);
				statuses = client.Status ();
			}
			Assert.That (statuses.ContainsKey ("foo"), "foo is no longer known");
			Assert.AreEqual (Status.Unknown, statuses ["foo"], "foo was not forgotten");
		}
		
		[Test]
		public void TestPull ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null);
				File.WriteAllText (file, "1");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (firstPath, secondPath);
				secondClient = new CommandClient (secondPath, null, null);
				Assert.AreEqual (1, secondClient.Log (null).Count, "Unexpected number of log entries");
				
				// Add changeset to original repo
				File.WriteAllText (file, "2");
				firstClient.Commit ("2");
				
				// Pull from clone
				Assert.IsTrue (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.AreEqual (2, secondClient.Log (null).Count, "Unexpected number of log entries");
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}
		
		[Test]
		public void TestMerge ()
		{
			string firstPath = GetTemporaryPath ();
			string secondPath = GetTemporaryPath ();
			string file = Path.Combine (firstPath, "foo");
			CommandClient.Initialize (firstPath);
			CommandClient firstClient = null,
			              secondClient = null;
			
			try {
				// Create repo with one commit
				firstClient = new CommandClient (firstPath, null, null);
				File.WriteAllText (file, "1\n");
				firstClient.Add (file);
				firstClient.Commit ("1");
			
				// Clone repo
				CommandClient.Clone (firstPath, secondPath);
				secondClient = new CommandClient (secondPath, null, null);
				Assert.AreEqual (1, secondClient.Log (null).Count, "Unexpected number of log entries");
				
				// Add changeset to original repo
				File.WriteAllText (file, "2\n");
				firstClient.Commit ("2");
				
				// Add non-conflicting changeset to child repo
				File.WriteAllText (Path.Combine (secondPath, "foo"), "1\na\n");
				secondClient.Commit ("a");
				
				// Pull from clone
				Assert.IsTrue (secondClient.Pull (null), "Pull unexpectedly resulted in unresolved files");
				Assert.AreEqual (3, secondClient.Log (null).Count, "Unexpected number of log entries");
				
				Assert.IsTrue (secondClient.Merge (null), "Merge unexpectedly resulted in unresolved files");
			} finally {
				if (null != firstClient)
					firstClient.Dispose ();
				if (null != secondClient)
					secondClient.Dispose ();
			}
		}

		static string GetTemporaryPath ()
		{
			string path = Path.Combine (Path.GetTempPath (), DateTime.UtcNow.Ticks.ToString ());
			garbage.Add (path);
			return path;
		}
	}
}

