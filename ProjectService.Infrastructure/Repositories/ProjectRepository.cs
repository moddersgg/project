﻿using Microsoft.EntityFrameworkCore;
using ProjectService.Domain.Models;
using ProjectService.Domain.Repositories;
using ProjectService.Domain.Results;
using ProjectService.Infrastructure.Database;

namespace ProjectService.Infrastructure.Repositories;

public class ProjectRepository(ProjectServiceDbContext database) : IProjectRepository
{
    public async Task<PagedResult<Project>> Get(string query, ProjectOrder order, List<ProjectCategory>? categories,
        string[]? tags, int page, int pageSize, string? fromUserId = null, string? userId = null)
    {
        var queryable = database.Projects
            .AsNoTracking()
            .Include(project => project.Members)
            .Include(project => project.Tags)
            .Where(project => project.IsPublished || project.Members.Any(member => member.UserId == userId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            queryable = queryable.Where(p =>
                EF.Functions.ILike(p.Name, $"%{query}%") ||
                (p.Summary != null && EF.Functions.ILike(p.Summary, $"%{query}%")) ||
                (p.Tags.Count != 0 && p.Tags.Any(tag => EF.Functions.ILike(tag.Name, $"%{query}%"))));
        }

        if (fromUserId is not null)
        {
            queryable = queryable.Where(project => project.Members.Any(member => member.UserId == fromUserId));
        }

        queryable = order switch
        {
            ProjectOrder.Relevance => queryable.OrderByDescending(project => project.CreatedAt),
            ProjectOrder.Published => queryable.OrderByDescending(project => project.PublishedAt),
            ProjectOrder.Updated => queryable.OrderByDescending(project => project.UpdatedAt),
            _ => queryable
        };

        if (categories is not null && categories.Count != 0)
        {
            queryable = queryable.Where(project => categories.Contains(project.Category));
        }
        
        if (tags is not null && tags.Length != 0)
        {
            queryable = queryable.Where(project => project.Tags.Any(tag => tags.Contains(tag.Name)));
        }

        var projects = await queryable
            .Select(p => new Project
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Summary = p.Summary,
                ImageUrl = p.ImageUrl,
                IsPublished = p.IsPublished,
                Category = p.Category,
                Tags = p.Tags,
            })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var totalCount = await queryable.CountAsync();

        return new PagedResult<Project>
        {
            Data = projects,
            Success = true,
            Message = "Projects retrieved successfully",
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<Project?> GetById(Ulid id, string? userId = null, bool force = false)
    {
        return await database.Projects
            .AsNoTracking()
            .Include(project => project.Members)
            .Include(project => project.Tags)
            .Include(project => project.Links)
            .Where(project => project.Id == id)
            .FirstOrDefaultAsync(project =>
                force || project.IsPublished || project.Members.Any(member => member.UserId == userId));
    }

    public async Task<Project?> GetBySlug(string slug, string? userId = null, bool force = false)
    {
        return await database.Projects
            .AsNoTracking()
            .Include(project => project.Members)
            .Include(project => project.Tags)
            .Include(project => project.Links)
            .Where(project => project.Slug == slug)
            .FirstOrDefaultAsync(project =>
                force || project.IsPublished || project.Members.Any(member => member.UserId == userId));
    }

    public async Task<Project?> Create(Project project)
    {
        // Detach tags from the project initially to avoid tracking conflicts
        var tags = project.Tags.ToList(); // Create a copy of the tags
        project.Tags.Clear(); // Clear the original collection to avoid duplicates

        // Add tags to database if they don't exist, and reattach them to the project
        foreach (var tag in tags)
        {
            var existingTag = await database.ProjectTags.FirstOrDefaultAsync(t => t.Name == tag.Name);
            if (existingTag is not null)
            {
                // Use the existing tag instance from the database
                project.Tags.Add(existingTag);
            }
            else
            {
                // Add new tag and attach it to the project
                var createdTag = await database.ProjectTags.AddAsync(tag);
                project.Tags.Add(createdTag.Entity);
            }
        }

        // Add the project to the database
        var createdProject = await database.Projects.AddAsync(project);
        await database.SaveChangesAsync();

        return createdProject.Entity;
    }
}