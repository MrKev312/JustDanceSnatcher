namespace JustDanceSnatcher.Interaction;

public static class SystemInteraction
{
	public static void SetClipboardText(string text)
	{
		Thread thread = new(() => Clipboard.SetText(text));
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		thread.Join();
	}

	public static async Task SendCommandViaClipboard(string command)
	{
		SetClipboardText(command);
		try
		{
			await Task.Delay(200); // Brief pause
			SendKeys.SendWait("^v"); // Ctrl+V
			await Task.Delay(100); // Pause
			SendKeys.SendWait("{ENTER}");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"ERROR: Failed to send keys for command ('{command}'). Ensure Discord is the active window. Details: {ex.Message}");
			Console.WriteLine("You may need to paste and send the command manually.");
		}
	}
}