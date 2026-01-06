using System;
using System.Collections.Generic;
using TodoListApp.Models;

namespace TodoListApp.Services
{
    public interface ITodoService
    {
        IEnumerable<TodoItem> GetAll(Guid userId);
        TodoItem? GetById(Guid id, Guid userId);
        void Create(TodoItem item);
        void Update(TodoItem item, Guid userId);
        void Delete(Guid id, Guid userId);
        void ToggleComplete(Guid id, Guid userId);
    }
}
