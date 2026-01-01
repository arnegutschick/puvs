using Terminal.Gui;

namespace ChatClient;

public class ChatUI
{
    private readonly ChatLogic _logic;
    private ScrollView _messagesScroll = null!;
    private int _messageCount = 0;
    private TextField _inputField = null!;

    public ChatUI(ChatLogic logic)
    {
        _logic = logic;
    }

    /// <summary>
    /// Runs the chat interface provided by the Terminal.Gui library
    /// </summary>
    public void Run()
    {
        Application.Init();
        var top = Application.Top;
        int inputFrameHeight = 4;

        // Declare chat window
        var win = new Window($"Chat - {_logic.Username}")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Declare view for text messages
        _messagesScroll = new ScrollView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - inputFrameHeight,
            ShowVerticalScrollIndicator = true,
            ColorScheme = new ColorScheme { Normal = Application.Driver.MakeAttribute(Color.Black, Color.White) },
        };

        // Declare message input field
        var inputFrame = new FrameView("Your Message")
        {
            X = 0,
            Y = Pos.Bottom(_messagesScroll),
            Width = Dim.Fill(),
            Height = inputFrameHeight
        };

        _inputField = new TextField
        {
            X = 1,
            Y = (inputFrameHeight - 1) / 2,
            Width = Dim.Fill() - 2
        };

        inputFrame.Add(_inputField);
        win.Add(_messagesScroll, inputFrame);

        // set focus to input field while typing
        _inputField.KeyPress += OnInputKeyPress;
        _inputField.SetFocus();

        // Import pub/sub message logic
        _logic.SubscribeEvents();

        // Start heartbeat Timer
        _logic.StartHeartbeat();

        top.Add(win);
        Application.Run();
    }

    /// <summary>
    /// Handles key press events in the input field.
    /// </summary>
    private async void OnInputKeyPress(View.KeyEventEventArgs e)
    {
        if (e.KeyEvent.Key != Key.Enter) return;

        string text = _inputField.Text?.ToString() ?? "";
        _inputField.Text = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            e.Handled = true;
            return;
        }

        // --- Commands start with "/" ---
        if (text.StartsWith("/"))
        {
            string command = text.Split(' ', 2)[0].ToLowerInvariant();

            switch (command)
            {
                case "/quit":
                    _logic.StopHeartbeat();
                    Application.RequestStop();
                    break;

                case "/stats":
                    await _logic.HandleStatisticsCommand();
                    break;

                case "/sendfile":
                    string filePath = text.Substring(10).Trim();
                    await _logic.HandleSendFile(filePath);
                    break;

                case "/msg":
                    if (text.Length <= 5)
                    {
                        AppendMessage("[ERROR] Usage: /msg <username> <message>", "red");
                        break;
                    }

                    string payload = text.Substring(5).Trim();
                    string[] parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2)
                    {
                        AppendMessage("[ERROR] Usage: /msg <username> <message>", "red");
                    }
                    else
                    {
                        _logic.SendPrivateMessage(parts[0], parts[1]);
                    }
                    break;

                case "/help":
                    AppendMessage("[INFO] Available commands: /quit, /stats, /sendfile <path>, /msg <user> <msg>, /help, /time", "Black");
                    break;

                case "/time":
                    await _logic.HandleTimeCommand();
                    break;

                default:
                    AppendMessage($"[ERROR] Unknown command '{command}'. Use /help for a list of commands.", "red");
                    break;
            }

            e.Handled = true;
            return;
        }

        // --- Public message ---
        _logic.SendMessage(text);
        e.Handled = true;
    }


    /// <summary>
    /// Adds a posted message to the message display field
    /// </summary>
    public void AppendMessage(string message, string colorName)
    {
        var color = MapColor(colorName);
        var attr = Application.Driver.MakeAttribute(color, Color.White);
        var cs = new ColorScheme { Normal = attr };

        int width = _messagesScroll.Frame.Width - 1;

        var lines = WrapText(message, width);

        foreach (var line in lines)
        {
            var label = new Label(line)
            {
                X = 1,
                Y = _messageCount,
                Width = line.Length
            };
            label.ColorScheme = cs;

            _messagesScroll.Add(label);
            _messageCount++;
        }

        _messagesScroll.ContentSize = new Size(
            Math.Max(_messagesScroll.ContentSize.Width, width),
            _messageCount
        );
        _messagesScroll.SetNeedsDisplay();
    }


    /// <summary>
    /// Splits a given text into multiple lines so that each line does not exceed the specified maximum width.
    /// This is useful for displaying messages in a fixed-width terminal UI, ensuring text wraps correctly
    /// within the available horizontal space.
    /// </summary>
    /// <param name="text">The text to be wrapped into multiple lines.</param>
    /// <param name="maxWidth">The maximum number of characters allowed per line.</param>
    /// <returns>A list of strings, each representing a line of text that fits within the given width.</returns>
    private static List<string> WrapText(string text, int maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ');

        string currentLine = "";

        foreach (var word in words)
        {
            if ((currentLine + " " + word).Trim().Length > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                if (currentLine.Length > 0)
                    currentLine += " ";
                currentLine += word;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
            lines.Add(currentLine);

        return lines;
    }


    /// <summary>
    /// Custom dialog when user receives a sent file. Allows the user to save the sent file.
    /// </summary>
    public Dialog ShowSaveFileDialog(BroadcastFileEvent file)
    {
        var dialog = new Dialog("File received", 60, 7);

        var label = new Label($"Save file '{file.FileName}'?") { X = 1, Y = 1 };
        var yesButton = new Button("Yes") { X = 10, Y = 3 };
        yesButton.Clicked += async () =>
        {
            await _logic.SaveFile(file);
            _inputField.SetFocus();
            Application.RequestStop();
        };

        var noButton = new Button("No") { X = 20, Y = 3 };
        noButton.Clicked += () =>
        {
            _inputField.SetFocus();
            Application.RequestStop();
        };

        dialog.Add(label, yesButton, noButton);
        return dialog;
    }

    private static Color MapColor(string name)
    {
        return name?.ToLowerInvariant() switch
        {
            "blue" => Color.Blue,
            "green" => Color.Green,
            "magenta" => Color.Magenta,
            "cyan" => Color.Cyan,
            "red" => Color.Red,
            "brown" => Color.Brown,
            "brightmagenta" => Color.BrightMagenta,
            "brightred" => Color.BrightRed,
            "brightblue" => Color.BrightBlue,
            "brightcyan" => Color.BrightCyan,
            "brightgreen" => Color.BrightGreen,
            "brightyellow" => Color.BrightYellow,
            "black" => Color.Black,
            _ => Color.White
        };
    }
}
