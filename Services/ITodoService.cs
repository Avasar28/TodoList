using System;
using System.Collections.Generic;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface ITodoService
    {
        IEnumerable<TodoItem> GetAll(int userId);
        TodoItem? GetById(Guid id, int userId);
        void Create(TodoItem item);
        void Update(TodoItem item, int userId);
        void Delete(Guid id, int userId);
        void ToggleComplete(Guid id, int userId);
    }
}
