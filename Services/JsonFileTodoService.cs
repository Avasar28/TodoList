using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public class JsonFileTodoService : ITodoService
    {
        private readonly string _filePath = "todos.json";

        public JsonFileTodoService(IWebHostEnvironment webHostEnvironment)
        {
             _filePath = Path.Combine(Directory.GetCurrentDirectory(), "todos.json");
             if (!File.Exists(_filePath))
             {
                 File.WriteAllText(_filePath, "[]");
             }
        }

        private List<TodoItem> ReadFromFile()
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<TodoItem>>(json) ?? new List<TodoItem>();
            }
            catch
            {
                return new List<TodoItem>();
            }
        }

        private void WriteToFile(List<TodoItem> items)
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        public IEnumerable<TodoItem> GetAll(int userId)
        {
            return ReadFromFile()
                .Where(x => x.UserId == userId)
                .OrderByDescending(t => t.CreatedAt);
        }

        public TodoItem? GetById(Guid id, int userId)
        {
            return ReadFromFile().FirstOrDefault(x => x.Id == id && x.UserId == userId);
        }

        public void Create(TodoItem item)
        {
            var items = ReadFromFile();
            item.Id = Guid.NewGuid();
            item.CreatedAt = DateTime.Now;
            // UserId should already be set on the item before calling this
            items.Add(item);
            WriteToFile(items);
        }

        public void Update(TodoItem item, int userId)
        {
            var items = ReadFromFile();
            var existing = items.FirstOrDefault(x => x.Id == item.Id && x.UserId == userId);
            if (existing != null)
            {
                existing.Title = item.Title;
                existing.IsCompleted = item.IsCompleted;
                existing.DueDate = item.DueDate;
                existing.Priority = item.Priority;
                WriteToFile(items);
            }
        }

        public void Delete(Guid id, int userId)
        {
            var items = ReadFromFile();
            var itemToRemove = items.FirstOrDefault(x => x.Id == id && x.UserId == userId);
            if (itemToRemove != null)
            {
                items.Remove(itemToRemove);
                WriteToFile(items);
            }
        }

        public void ToggleComplete(Guid id, int userId)
        {
            var items = ReadFromFile();
            var item = items.FirstOrDefault(x => x.Id == id && x.UserId == userId);
            if (item != null)
            {
                item.IsCompleted = !item.IsCompleted;
                WriteToFile(items);
            }
        }
    }
}
