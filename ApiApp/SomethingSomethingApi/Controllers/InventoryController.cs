using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using SomethingSomethingApi.Models;

namespace SomethingSomethingApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public InventoryController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // GET api/inventory
    // Returns the main inventory list that the MAUI page binds into the on-screen CollectionView.
    [HttpGet]
    public IActionResult GetInventory()
    {
        var items = new List<InventoryItemDto>();
        var connectionString = _configuration.GetConnectionString("InventoryDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string sql = """
                SELECT ItemId, Name, Quantity, Category
                FROM InventoryItems
                ORDER BY Name ASC;
                """;

            using var command = new MySqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                items.Add(new InventoryItemDto
                {
                    ItemId = reader.GetInt32("ItemId"),
                    Name = reader.GetString("Name"),
                    Quantity = reader.GetInt32("Quantity"),
                    Category = reader.GetString("Category")
                });
            }

            return Ok(items);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // POST api/inventory
    // Creates one new inventory row after the MAUI add form sends validated JSON.
    [HttpPost]
    public IActionResult CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("Category is required.");
        }

        var connectionString = _configuration.GetConnectionString("InventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string insertSql = """
                INSERT INTO InventoryItems (Name, Quantity, Category)
                VALUES (@Name, @Quantity, @Category);
                SELECT LAST_INSERT_ID();
                """;

            using var command = new MySqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("@Name", request.Name.Trim());
            command.Parameters.AddWithValue("@Quantity", request.Quantity);
            command.Parameters.AddWithValue("@Category", request.Category.Trim());

            var newId = Convert.ToInt32(command.ExecuteScalar());

            var createdItem = new InventoryItemDto
            {
                ItemId = newId,
                Name = request.Name.Trim(),
                Quantity = request.Quantity,
                Category = request.Category.Trim()
            };

            return Ok(createdItem);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // PUT api/inventory/5
    // Updates one existing inventory row when the MAUI form is in edit mode.
    [HttpPut("{id:int}")]
    public IActionResult UpdateInventoryItem(int id, [FromBody] CreateInventoryItemRequest request)
    {
        if (id <= 0)
        {
            return BadRequest("A valid item id is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest("Category is required.");
        }

        var connectionString = _configuration.GetConnectionString("InventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string updateSql = """
                UPDATE InventoryItems
                SET Name = @Name,
                    Quantity = @Quantity,
                    Category = @Category
                WHERE ItemId = @ItemId;
                """;

            using var command = new MySqlCommand(updateSql, connection);
            command.Parameters.AddWithValue("@Name", request.Name.Trim());
            command.Parameters.AddWithValue("@Quantity", request.Quantity);
            command.Parameters.AddWithValue("@Category", request.Category.Trim());
            command.Parameters.AddWithValue("@ItemId", id);

            var rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                return NotFound($"No inventory item with id {id} was found.");
            }

            var updatedItem = new InventoryItemDto
            {
                ItemId = id,
                Name = request.Name.Trim(),
                Quantity = request.Quantity,
                Category = request.Category.Trim()
            };

            return Ok(updatedItem);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // DELETE api/inventory/5
    // Removes one inventory row after the user confirms deletion in the mobile app.
    [HttpDelete("{id:int}")]
    public IActionResult DeleteInventoryItem(int id)
    {
        if (id <= 0)
        {
            return BadRequest("A valid item id is required.");
        }

        var connectionString = _configuration.GetConnectionString("InventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string deleteSql = """
                DELETE FROM InventoryItems
                WHERE ItemId = @ItemId;
                """;

            using var command = new MySqlCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@ItemId", id);

            var rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected == 0)
            {
                return NotFound($"No inventory item with id {id} was found.");
            }

            return Ok(new
            {
                message = $"Inventory item {id} deleted successfully."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // GET api/inventory/5/notes
    // Returns note history for one inventory item so the details page can render it.
    [HttpGet("{id:int}/notes")]
    public IActionResult GetInventoryNotes(int id)
    {
        if (id <= 0)
        {
            return BadRequest("A valid item id is required.");
        }

        var connectionString = _configuration.GetConnectionString("InventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string itemCheckSql = """
                SELECT COUNT(*)
                FROM InventoryItems
                WHERE ItemId = @ItemId;
                """;

            using (var itemCheckCommand = new MySqlCommand(itemCheckSql, connection))
            {
                itemCheckCommand.Parameters.AddWithValue("@ItemId", id);
                var itemExists = Convert.ToInt32(itemCheckCommand.ExecuteScalar()) > 0;
                if (!itemExists)
                {
                    return NotFound($"No inventory item with id {id} was found.");
                }
            }

            const string notesSql = """
                SELECT NoteId, ItemId, NoteText, CreatedAt
                FROM InventoryItemNotes
                WHERE ItemId = @ItemId
                ORDER BY CreatedAt DESC, NoteId DESC;
                """;

            var notes = new List<InventoryNoteDto>();
            using var notesCommand = new MySqlCommand(notesSql, connection);
            notesCommand.Parameters.AddWithValue("@ItemId", id);

            using var reader = notesCommand.ExecuteReader();
            while (reader.Read())
            {
                notes.Add(new InventoryNoteDto
                {
                    NoteId = reader.GetInt32("NoteId"),
                    ItemId = reader.GetInt32("ItemId"),
                    NoteText = reader.GetString("NoteText"),
                    CreatedAt = reader.GetDateTime("CreatedAt")
                });
            }

            return Ok(notes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }

    // POST api/inventory/5/notes
    // Saves a new note for one inventory item, then returns the created note with its timestamp.
    [HttpPost("{id:int}/notes")]
    public IActionResult CreateInventoryNote(int id, [FromBody] CreateInventoryNoteRequest request)
    {
        if (id <= 0)
        {
            return BadRequest("A valid item id is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.NoteText))
        {
            return BadRequest("Note text is required.");
        }

        var connectionString = _configuration.GetConnectionString("InventoryDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(500, "InventoryDb connection string is missing.");
        }

        try
        {
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            const string itemCheckSql = """
                SELECT COUNT(*)
                FROM InventoryItems
                WHERE ItemId = @ItemId;
                """;

            using (var itemCheckCommand = new MySqlCommand(itemCheckSql, connection))
            {
                itemCheckCommand.Parameters.AddWithValue("@ItemId", id);
                var itemExists = Convert.ToInt32(itemCheckCommand.ExecuteScalar()) > 0;
                if (!itemExists)
                {
                    return NotFound($"No inventory item with id {id} was found.");
                }
            }

            const string insertNoteSql = """
                INSERT INTO InventoryItemNotes (ItemId, NoteText)
                VALUES (@ItemId, @NoteText);
                SELECT LAST_INSERT_ID();
                """;

            var trimmedNoteText = request.NoteText.Trim();
            using var insertCommand = new MySqlCommand(insertNoteSql, connection);
            insertCommand.Parameters.AddWithValue("@ItemId", id);
            insertCommand.Parameters.AddWithValue("@NoteText", trimmedNoteText);

            var newNoteId = Convert.ToInt32(insertCommand.ExecuteScalar());

            const string selectCreatedNoteSql = """
                SELECT NoteId, ItemId, NoteText, CreatedAt
                FROM InventoryItemNotes
                WHERE NoteId = @NoteId;
                """;

            using var selectCommand = new MySqlCommand(selectCreatedNoteSql, connection);
            selectCommand.Parameters.AddWithValue("@NoteId", newNoteId);

            using var reader = selectCommand.ExecuteReader();
            if (!reader.Read())
            {
                return StatusCode(500, "The note was saved, but it could not be reloaded.");
            }

            var createdNote = new InventoryNoteDto
            {
                NoteId = reader.GetInt32("NoteId"),
                ItemId = reader.GetInt32("ItemId"),
                NoteText = reader.GetString("NoteText"),
                CreatedAt = reader.GetDateTime("CreatedAt")
            };

            return Ok(createdNote);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Database error: {ex.Message}");
        }
    }
}
