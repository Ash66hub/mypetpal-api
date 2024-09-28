﻿using Microsoft.EntityFrameworkCore;
using mypetpal.Services.Contracts;
using mypetpal.Models;
using mypetpal.dbContext;

namespace mypetpal.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User> CreateNewUser(string username, string email, string password)
        {
            if (_context.Users.Any(u => u.Username == username || u.Email == email))
            {
                throw new ArgumentException("Username or Email already exists.");
            }

            var userMetadata = new UserMetadata();
            userMetadata.Metadata_createdUtc = DateTime.Now;

            var user = new User
            {
                Username = username,
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                Metadata = System.Text.Json.JsonSerializer.Serialize(userMetadata)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            return await _context.Users.Select(u => new User
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email
            }).ToListAsync();
        }

        public async Task<User> GetUserById(long userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            return user;
        }

        public async Task<User> GetUserByUsername(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            return user;
        }

        public async Task<User> UpdateUser(long userId, string username, string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            user.Username = username ?? user.Username;
            user.Email = email ?? user.Email;

            if (!string.IsNullOrEmpty(password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
            }

            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<bool> DeleteUser(long userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task SaveRefreshToken(long userId, string refreshToken) //not working
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }
    }
}