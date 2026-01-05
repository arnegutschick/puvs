using Terminal.Gui;
using Chat.Contracts;

namespace ChatClient;


/// <summary>
/// Represents the terminal user interface layer of the chat application.
/// This class manages layout, input handling, and delegates all
/// business logic to <see cref="ChatLogic"/>.
/// </summary>
public class ChatUI
{
    /// Provides the core chat logic and handles communication and command processing
    private readonly ChatLogic _logic;

    /// Scrollable view that displays all chat messages
    private ScrollView _messagesScroll = null!;

    /// Tracks the total number of messages currently displayed in the message view
    /// Used to calculate vertical positioning and content size
    private int _messageCount = 0;

    /// Text input field used for entering chat messages and commands
    private TextField _inputField = null!;

    // Constructor
    public ChatUI(ChatLogic logic)
    {
        _logic = logic;
    }


    /// <summary>
    /// Initializes and runs the terminal-based chat user interface.
    /// Sets up the main window, message display, input field, event handling,
    /// and starts the application event loop.
    /// </summary>
    public void Run()
    {
        // Initialize the terminal application
        Application.Init();
        var top = Application.Top;
        int inputFrameHeight = 4; // Height of the message input area

        // --- Main chat window ---
        var win = new Window($"Chat - {_logic.Username}")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),   // Fill the entire horizontal space of the terminal
            Height = Dim.Fill()   // Fill the entire vertical space
        };

        // --- Scrollable view for chat messages ---
        _messagesScroll = new ScrollView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - inputFrameHeight,
            ShowVerticalScrollIndicator = false,
            ShowHorizontalScrollIndicator = false,
            ColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.Black, Color.White) // Default text colors
            },
        };

        // --- Frame containing the input field ---
        var inputFrame = new FrameView("Your Message")
        {
            X = 0,
            Y = Pos.Bottom(_messagesScroll),
            Width = Dim.Fill(),
            Height = inputFrameHeight
        };

        // --- Text field for typing messages ---
        _inputField = new TextField
        {
            X = 1,
            Y = (inputFrameHeight - 1) / 2,
            Width = Dim.Fill() - 2
        };

        inputFrame.Add(_inputField);
        win.Add(_messagesScroll, inputFrame);

        // --- Event handling ---
        _inputField.KeyPress += OnInputKeyPress; // Handle Enter key and commands
        _inputField.SetFocus();                  // Focus input immediately

        // --- Start backend logic ---
        _logic.SubscribeEvents();  // Listen for incoming messages/events
        _logic.StartClientHeartbeat();   // Start periodic heartbeat for connection monitoring
        _ = _logic.HandleUsersCommandAsync(); // Display user list after joining

        // Add main window and start the application loop
        top.Add(win);
        Application.Run();
    }


    /// <summary>
    /// Handles key press events for the input field and processes user input when the Enter key is pressed.
    /// Supports chat commands prefixed with '/' as well as sending public messages.
    /// </summary>
    /// <param name="e">
    /// The key event arguments containing the pressed key and handling state.
    /// </param>
    private async void OnInputKeyPress(View.KeyEventEventArgs e)
    {
        try
        {
            // Only handle Enter key; ignore other keys
            if (e.KeyEvent.Key != Key.Enter) return;

            // Get input text and clear the field
            string text = _inputField.Text?.ToString() ?? "";
            _inputField.Text = "";

            // Ignore empty or whitespace-only input
            if (string.IsNullOrWhiteSpace(text))
            {
                e.Handled = true;
                return;
            }

            // --- Commands start with "/" ---
            if (text.StartsWith("/"))
            {
                string command = text.Split(' ', 2)[0].ToLowerInvariant(); // Extract command

                switch (command)
                {
                    case "/quit":
                        // Stop heartbeat and close the application
                        _logic.StopClientHeartbeat();
                        Application.RequestStop();
                        break;

                    case "/stats":
                        // Handle /stats command
                        try
                        {
                            await _logic.HandleStatisticsCommandAsync();
                        }
                        catch (Exception)
                        {
                            AppendMessage("[ERROR] Failed to retrieve chat statistics. Please try again.", "red");
                        }
                        break;

                    case "/sendfile":
                    // Handle /sendfile command
                        try
                        {
                            // Check if the message contains a filepath
                            string filePath = text.Length > 9 ? text.Substring(9).Trim() : "";

                            if (string.IsNullOrWhiteSpace(filePath))
                            {
                                AppendMessage("[ERROR] Usage: /sendfile <path> - Please provide a file path.", "red");
                                break;
                            }

                            await _logic.HandleSendFileAsync(filePath);
                        }
                        catch (Exception)
                        {
                            AppendMessage("[ERROR] Failed to send file. Please try again.", "red");
                        }
                        break;

                    case "/msg":
                        // Private message command: /msg <username> <message>
                        try
                        {
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
                                // Send private message to the specified user
                                await _logic.SendPrivateMessageAsync(parts[0], parts[1]);
                            }
                            break;
                        }
                        catch (Exception)
                        {
                            AppendMessage($"[ERROR] Failed to send private message: Please try again.", "red");
                        }
                        break;

                    case "/time":
                        // Handle /time command
                        try
                        {
                            await _logic.HandleTimeCommandAsync();
                            break;
                        }
                        catch (Exception)
                        {
                            AppendMessage($"[ERROR] Failed to request current time. Please try again.", "red");
                        }
                        break;

                    case "/users":
                        // Handle /users command
                        try
                        {
                            await _logic.HandleUsersCommandAsync();
                            break;
                        }
                        catch (Exception)
                        {
                            AppendMessage($"[ERROR] Failed to request currently logged in users. Please try again.", "red");
                        }
                        break;

                    case "/help":
                        // Display available commands - using seperate messages for each command to ensure correct message display in chat UI
                        AppendMessage("[INFO] Available commands:", "Black");
                        AppendMessage("/quit        - Quit the chat application", "Black");
                        AppendMessage("/stats       - Show chat statistics", "Black");
                        AppendMessage("/sendfile <path> - Send a file to all users", "Black");
                        AppendMessage("/msg <user> <msg> - Send a private message to a user", "Black");
                        AppendMessage("/time        - Show current server time", "Black");
                        AppendMessage("/users       - Show a list of currently logged in users", "Black");
                        AppendMessage("/help        - Show this help message", "Black");
                        break;


                    default:
                        // Handle unknown command
                        AppendMessage($"[ERROR] Unknown command '{command}'. Use /help for a list of commands.", "red");
                        break;
                }

                e.Handled = true; // Prevent further processing
                return;
            }

            // Send normal, public message to all users
            await _logic.SendMessageAsync(text);
            e.Handled = true;
        }
        catch (Exception)
        {
            // Log and show error in chat window
            AppendMessage($"[ERROR] An exception occured while processing input.", "red");
        }
    }


    /// <summary>
    /// Appends a message to the message view, wrapping the text to the available width
    /// and rendering it using the specified foreground color.
    /// </summary>
    /// <param name="message">
    /// The message text to append.
    /// </param>
    /// <param name="colorName">
    /// The name of the foreground color used to render the message.
    /// </param>
    public void AppendMessage(string message, string colorName)
    {
        // Map server-sided color name to Color enum and create a color attribute
        var color = MapColor(colorName);
        var attr = Application.Driver.MakeAttribute(color, Color.White);
        var cs = new ColorScheme { Normal = attr };

        // Determine the maximum width for wrapping
        int width = _messagesScroll.Frame.Width - 3;

        // Wrap the message text into lines that fit the width
        var lines = WrapText(message, width);

        // Add each line as a separate Label to the ScrollView for custom text coloring
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

        // Update the scrollable content size
        _messagesScroll.ContentSize = new Size(
            Math.Max(_messagesScroll.ContentSize.Width, width), // Adjust width if needed
            _messageCount                                      // Height based on total lines
        );

        // Refresh the ScrollView to display new content
        _messagesScroll.SetNeedsDisplay();

        // Automatically scroll to the bottom after sending a message
        _messagesScroll.ScrollDown(_messageCount - _messagesScroll.Frame.Height + 1);
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
            // Check if adding the next word would exceed maxWidth
            if ((currentLine + " " + word).Trim().Length > maxWidth)
            {
                // Add the current line to the result and start a new line with the word
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                // Add a space if the line already has words
                if (currentLine.Length > 0)
                    currentLine += " ";

                currentLine += word; // Append the word to the current line
            }
        }

        // Add the last line if it contains text
        if (!string.IsNullOrWhiteSpace(currentLine))
            lines.Add(currentLine);

        return lines;
    }


    /// <summary>
    /// Displays a dialog prompting the user to save a received file.
    /// </summary>
    /// <param name="file">
    /// The broadcast file event containing the file information to be saved.
    /// </param>
    /// <returns>
    /// A <see cref="Dialog"/> that allows the user to confirm or cancel saving the file.
    /// </returns>
    public Dialog ShowSaveFileDialog(BroadcastFileEvent file)
    {
        // --- Dynamically set dialog width to terminal width (max 65 for readability) ---
        int terminalWidth = Application.Driver.Cols;
        int dialogWidth = Math.Min(terminalWidth - 5, 65); // leave some margin
        int dialogHeight = 7;

        // --- Create base color schemes ---
        var normalAttr = Application.Driver.MakeAttribute(Color.Black, Color.White);
        var focusedAttr = Application.Driver.MakeAttribute(Color.White, Color.Black);

        var normalCS = new ColorScheme { Normal = normalAttr };
        var focusedCS = new ColorScheme { Normal = focusedAttr };

        var dialog = new Dialog("File received", dialogWidth, dialogHeight);

        // --- Center the label in the dialog ---
        var labelText = $"Save file '{file.FileName}'?";
        int labelX = Math.Max((dialogWidth - labelText.Length) / 2, 1);
        var label = new Label(labelText)
        {
            X = labelX,
            Y = 1
        };

        // --- Yes button ---
        var yesButton = new Button("Yes")
        {
            X = dialogWidth / 2 - 10,
            Y = 3
        };
        yesButton.Clicked += async () =>
        {
            try
            {
                await _logic.SaveFileAsync(file); // Save file asynchronously
            }
            catch (Exception)
            {
                AppendMessage($"[ERROR] Failed to save file. Please ask the user to resend the file.", "red");
            }
            finally
            {
                _inputField.SetFocus();       // Return focus to input field
                Application.RequestStop();    // Close the dialog
            }
        };

        // --- No button ---
        var noButton = new Button("No")
        {
            X = dialogWidth / 2 + 2,
            Y = 3,
        };
        noButton.Clicked += () =>
        {
            _inputField.SetFocus();
            Application.RequestStop();
        };

        // Add all controls to the dialog
        dialog.Add(label, yesButton, noButton);

        return dialog;
    }

    /// <summary>
    /// Maps a color name string to the corresponding <see cref="Color"/> value.
    /// Returns <see cref="Color.Black"/> if the name is null or not recognized.
    /// </summary>
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
            _ => Color.Black
        };
    }
}
