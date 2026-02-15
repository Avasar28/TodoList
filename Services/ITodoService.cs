using System;
using System.Collections.Generic;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface ITodoService
    {
        IEnumerable<TodoItem> GetAll(string userId);
        TodoItem? GetById(Guid id, string userId);
        void Create(TodoItem item);
        void Update(TodoItem item, string userId);
        void Delete(Guid id, string userId);
        void ToggleComplete(Guid id, string userId);
    }
}
