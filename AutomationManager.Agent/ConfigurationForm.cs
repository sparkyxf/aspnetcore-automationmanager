using System.Runtime.Versioning;
using System.Text.Json;

using System.Windows.Forms;
using Microsoft.Extensions.Configuration;

namespace AutomationManager.Agent;

[SupportedOSPlatform("windows6.1")]
public class ConfigurationForm : System.Windows.Forms.Form
{
    private System.Windows.Forms.TextBox txtApiBaseUrl = null!;
    private System.Windows.Forms.TextBox txtApiAuthToken = null!;
    private System.Windows.Forms.TextBox txtAgentName = null!;
    private System.Windows.Forms.TextBox txtAgentId = null!;
    private System.Windows.Forms.Button btnSave = null!;
    private System.Windows.Forms.Button btnCancel = null!;
    private readonly string _configFilePath;

    public ConfigurationForm(string configFilePath)
    {
        _configFilePath = configFilePath;
        InitializeComponent();
        LoadConfiguration();
    }

    private void InitializeComponent()
    {
        this.Text = "Agent Configuration";
        this.Size = new System.Drawing.Size(500, 300);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        // API Base URL
        var lblApiBaseUrl = new System.Windows.Forms.Label
        {
            Text = "API Base URL:",
            Location = new System.Drawing.Point(20, 20),
            Size = new System.Drawing.Size(120, 20)
        };
        txtApiBaseUrl = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(150, 18),
            Size = new System.Drawing.Size(300, 25),
            PlaceholderText = "http://localhost:5200"
        };

        // API Auth Token
        var lblApiAuthToken = new System.Windows.Forms.Label
        {
            Text = "API Auth Token:",
            Location = new System.Drawing.Point(20, 60),
            Size = new System.Drawing.Size(120, 20)
        };
        txtApiAuthToken = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(150, 58),
            Size = new System.Drawing.Size(300, 25),
            PlaceholderText = "Optional"
        };

        // Agent Name
        var lblAgentName = new System.Windows.Forms.Label
        {
            Text = "Agent Name:",
            Location = new System.Drawing.Point(20, 100),
            Size = new System.Drawing.Size(120, 20)
        };
        txtAgentName = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(150, 98),
            Size = new System.Drawing.Size(300, 25),
            PlaceholderText = Environment.MachineName
        };

        // Agent ID (read-only)
        var lblAgentId = new System.Windows.Forms.Label
        {
            Text = "Agent ID:",
            Location = new System.Drawing.Point(20, 140),
            Size = new System.Drawing.Size(120, 20)
        };
        txtAgentId = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(150, 138),
            Size = new System.Drawing.Size(300, 25),
            ReadOnly = true,
            BackColor = System.Drawing.SystemColors.Control
        };

        // Buttons
        btnSave = new System.Windows.Forms.Button
        {
            Text = "Save",
            Location = new System.Drawing.Point(250, 200),
            Size = new System.Drawing.Size(90, 30),
            DialogResult = System.Windows.Forms.DialogResult.OK
        };
        btnSave.Click += BtnSave_Click;

        btnCancel = new System.Windows.Forms.Button
        {
            Text = "Cancel",
            Location = new System.Drawing.Point(360, 200),
            Size = new System.Drawing.Size(90, 30),
            DialogResult = System.Windows.Forms.DialogResult.Cancel
        };

        this.Controls.AddRange(new System.Windows.Forms.Control[]
        {
            lblApiBaseUrl, txtApiBaseUrl,
            lblApiAuthToken, txtApiAuthToken,
            lblAgentName, txtAgentName,
            lblAgentId, txtAgentId,
            btnSave, btnCancel
        });

        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;
    }

    private void LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (config != null)
                {
                    if (config.TryGetValue("ApiBaseUrl", out var apiBaseUrl))
                        txtApiBaseUrl.Text = apiBaseUrl.GetString() ?? "";

                    if (config.TryGetValue("ApiAuthToken", out var authToken))
                        txtApiAuthToken.Text = authToken.GetString() ?? "";

                    if (config.TryGetValue("AgentName", out var agentName))
                        txtAgentName.Text = agentName.GetString() ?? "";

                    if (config.TryGetValue("AgentId", out var agentId))
                    {
                        var idValue = agentId.GetString();
                        if (string.IsNullOrEmpty(idValue))
                        {
                            // Auto-generate AgentId if not set
                            txtAgentId.Text = Guid.NewGuid().ToString();
                        }
                        else
                        {
                            txtAgentId.Text = idValue;
                        }
                    }
                    else
                    {
                        // Auto-generate AgentId if not present
                        txtAgentId.Text = Guid.NewGuid().ToString();
                    }
                }
            }
            else
            {
                // Auto-generate AgentId for new configuration
                txtAgentId.Text = Guid.NewGuid().ToString();
            }
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            // Auto-generate AgentId on error
            txtAgentId.Text = Guid.NewGuid().ToString();
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            // Validate API Base URL
            if (string.IsNullOrWhiteSpace(txtApiBaseUrl.Text))
            {
                System.Windows.Forms.MessageBox.Show("API Base URL is required.", "Validation Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            if (!Uri.TryCreate(txtApiBaseUrl.Text, UriKind.Absolute, out _))
            {
                System.Windows.Forms.MessageBox.Show("API Base URL must be a valid URL.", "Validation Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            // Read existing config
            var config = new Dictionary<string, object>();
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var existingConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (existingConfig != null)
                {
                    foreach (var kvp in existingConfig)
                    {
                        if (kvp.Key != "ApiBaseUrl" && kvp.Key != "ApiAuthToken" && kvp.Key != "AgentName" && kvp.Key != "AgentId")
                        {
                            config[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            // Update configuration values
            config["ApiBaseUrl"] = txtApiBaseUrl.Text.Trim();
            config["ApiAuthToken"] = txtApiAuthToken.Text.Trim();
            config["AgentName"] = txtAgentName.Text.Trim();
            config["AgentId"] = txtAgentId.Text.Trim();

            // Save to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var updatedJson = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configFilePath, updatedJson);

            System.Windows.Forms.MessageBox.Show("Configuration saved successfully!\n\nPlease restart the agent for changes to take effect.", 
                "Success", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }
}
