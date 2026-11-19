using BizHawk.Client.Common;

namespace BizHawk.Tests.Client.Common.js
{
	[DoNotParallelize]
	[TestClass]
	public class BasicJsTests
	{
		[TestMethod]
		public void V8IsAvailableOrSkippedCleanly()
		{
			if (!JsLibraries.IsAvailable)
			{
				Assert.Inconclusive($"V8 natives unavailable (skip): {JsLibraries.AvailabilityError}");
				return;
			}

			Assert.IsTrue(JsLibraries.IsAvailable);
		}

		[TestMethod]
		public void CanPrintAndReadFrameCount()
		{
			if (!JsLibraries.IsAvailable)
			{
				Assert.Inconclusive($"V8 natives unavailable (skip): {JsLibraries.AvailabilityError}");
				return;
			}

			JsTestContext context = new();
			context.AddScript("hello_framecount.js", true);
			context.RunYielding();
			Assert.IsTrue(context.loggedMessages.Count >= 2,
				"messages: " + string.Join(" || ", context.loggedMessages));
			Assert.AreEqual("hello", context.loggedMessages[0]);
			Assert.AreEqual("0", context.loggedMessages[1]);
		}

		[TestMethod]
		public void FrameAdvanceYieldsAndResumes()
		{
			if (!JsLibraries.IsAvailable)
			{
				Assert.Inconclusive($"V8 natives unavailable (skip): {JsLibraries.AvailabilityError}");
				return;
			}

			JsTestContext context = new();
			context.AddScript("frame_loop.js", true);
			context.RunYielding();
			Assert.AreEqual(JsFile.RunState.Running, context.GetScriptState(0));
			context.RunFrameWaiting();
			context.RunFrameWaiting();
			Assert.IsTrue(context.loggedMessages.Count >= 2);
			Assert.AreEqual("frame", context.loggedMessages[0]);
		}

		[TestMethod]
		public void EventOnFrameEndKeepsScriptRunning()
		{
			if (!JsLibraries.IsAvailable)
			{
				Assert.Inconclusive($"V8 natives unavailable (skip): {JsLibraries.AvailabilityError}");
				return;
			}

			JsTestContext context = new();
			context.AddScript("register_frame_end.js", true);
			context.RunYielding();
			Assert.AreEqual(JsFile.RunState.Running, context.GetScriptState(0));
			context.RunFrameWaiting();
			Assert.IsTrue(context.loggedMessages.Contains("tick"));
		}
	}
}
