using Terminal.Gui;

namespace ChatClient;

public class ChatUI
{
    private readonly ChatLogic _logic;
    private TextView _messagesView = null!;
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
        _messagesView = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - inputFrameHeight,
            ReadOnly = true,
            WordWrap = true
        };

        // Declare message input field
        var inputFrame = new FrameView("Your Message")
        {
            X = 0,
            Y = Pos.Bottom(_messagesView),
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
        win.Add(_messagesView, inputFrame);

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
    private void OnInputKeyPress(View.KeyEventEventArgs e)
    {
        if (e.KeyEvent.Key != Key.Enter) return;

        string text = _inputField.Text.ToString() ?? "";
        _inputField.Text = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            e.Handled = true;
            return;
        }

        // --- Quit chat ---
        if (text.Equals("/quit", StringComparison.OrdinalIgnoreCase))
        {
            // Stop heartbeat
            _logic.StopHeartbeat();

            // End client application
            Application.RequestStop();
            e.Handled = true;
            return;
        }

        // --- Send file ---
        if (text.StartsWith("/sendfile ", StringComparison.OrdinalIgnoreCase))
        {
            _logic.HandleSendFile(text.Substring(10).Trim());
            e.Handled = true;
            return;
        }

        // --- Private message ---
        if (text.StartsWith("/msg ", StringComparison.OrdinalIgnoreCase))
        {
            string payload = text.Substring(5).Trim();
            string[] parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                AppendMessage("[ERROR] Usage: /msg <username> <message>");
                e.Handled = true;
                return;
            }

            string recipient = parts[0];
            string message = parts[1];

            _logic.SendPrivateMessage(recipient, message);
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
    public void AppendMessage(string message)
            {
            _messagesView.Text += message + "\n";
                _messagesView.MoveEnd();
    }
    
    /// <summary>
    /// Custom dialog when user receives a sent file. Allows the user to save the sent file.
    /// </summary>
    public Dialog ShowSaveFileDialog(FileReceivedEvent file)
    {
        var dialog = new Dialog("File received", 60, 7);

        var label = new Label($"Save file '{file.FileName}'?") { X = 1, Y = 1 };
        var yesButton = new Button("Yes") { X = 10, Y = 3 };
        yesButton.Clicked += () =>
        {
            _logic.SaveFile(file);
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
}
