using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Learning.Features.Admin;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// Q-8 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). The point of this endpoint over the per-row
/// <c>PUT /admin/exercises/{id}</c> loop both editors used before is that a reorder cannot land
/// half-applied. What is actually testable at this level is the other half of that promise: every
/// way a request could produce duplicated or missing positions is refused <em>before</em> anything
/// is written, so there is no partial state for a caller to observe or recover from.
/// </summary>
[TestFixture]
public sealed class AdminExercisesReorderTests
{
    private static readonly Guid LessonId = new("11111111-0000-4000-8000-000000000001");
    private static readonly Guid OtherLessonId = new("11111111-0000-4000-8000-000000000002");

    private static AdminExercisesController CreateController(LearningDbContext databaseContext) =>
        new(databaseContext, NullLogger<AdminExercisesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = PlatformAdministrator() },
            },
        };

    /// <summary>
    /// <c>ContentAuthoringGuard.MayAuthor</c> lets an organization administrator edit its own rows
    /// and reserves the global library for platform staff. The fixtures below are global lessons
    /// (<c>OrganizationId = null</c>), so the principal has to be platform staff.
    /// </summary>
    private static ClaimsPrincipal PlatformAdministrator() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

    /// <summary>Three exercises at positions 0, 1, 2 in <see cref="LessonId"/>.</summary>
    private static async Task<LearningDbContext> SeedLessonWithThreeExercisesAsync()
    {
        var databaseContext = LearningDbContextFactory.CreateInMemory();

        databaseContext.Lessons.Add(new Lesson { Id = LessonId, Title = "Первый контакт", Slug = "first-contact" });
        databaseContext.Lessons.Add(new Lesson { Id = OtherLessonId, Title = "Другой урок", Slug = "other" });

        for (var position = 0; position < 3; position++)
        {
            databaseContext.Exercises.Add(new Exercise
            {
                Id = ExerciseIdAt(position),
                LessonId = LessonId,
                Type = "choose_option",
                OrderInLesson = position,
                SerializedContent = """{"question":"q"}""",
            });
        }

        databaseContext.Exercises.Add(new Exercise
        {
            Id = ForeignExerciseId,
            LessonId = OtherLessonId,
            Type = "choose_option",
            OrderInLesson = 0,
            SerializedContent = """{"question":"q"}""",
        });

        await databaseContext.SaveChangesAsync();
        return databaseContext;
    }

    private static Guid ExerciseIdAt(int position) =>
        new($"22222222-0000-4000-8000-00000000000{position}");

    private static readonly Guid ForeignExerciseId = new("33333333-0000-4000-8000-000000000001");

    private static async Task<List<(Guid Id, int Order)>> ReadOrderAsync(LearningDbContext databaseContext) =>
        (await databaseContext.Exercises
            .Where(exercise => exercise.LessonId == LessonId)
            .OrderBy(exercise => exercise.OrderInLesson)
            .ToListAsync())
        .Select(exercise => (exercise.Id, exercise.OrderInLesson))
        .ToList();

    [Test]
    public async Task Reorder_moves_a_row_past_its_neighbour_and_returns_the_new_order()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        // What the ▲ button on the third row means: 0,1,2 -> the third exercise becomes second.
        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
            new ExerciseOrderDto(ExerciseIdAt(2), 1),
            new ExerciseOrderDto(ExerciseIdAt(1), 2),
        ]));

        var returned = (response.Result as OkObjectResult)?.Value as IReadOnlyList<AdminExerciseDto>;
        returned.Should().NotBeNull();
        returned!.Select(exercise => exercise.Id).Should()
            .Equal(ExerciseIdAt(0), ExerciseIdAt(2), ExerciseIdAt(1));

        // Persisted, not just echoed back.
        var stored = await ReadOrderAsync(databaseContext);
        stored.Select(row => row.Id).Should().Equal(ExerciseIdAt(0), ExerciseIdAt(2), ExerciseIdAt(1));
    }

    [Test]
    public async Task Reorder_refuses_a_subset_and_writes_nothing()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        // Naming only the two swapped rows is exactly how the per-row loop used to work, and it is
        // what cannot be validated: position 1 might already be held by the row left unmentioned.
        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 1),
            new ExerciseOrderDto(ExerciseIdAt(1), 0),
        ]));

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        var stored = await ReadOrderAsync(databaseContext);
        stored.Select(row => row.Order).Should().Equal(0, 1, 2);
        stored.Select(row => row.Id).Should().Equal(ExerciseIdAt(0), ExerciseIdAt(1), ExerciseIdAt(2));
    }

    [Test]
    public async Task Reorder_refuses_two_exercises_at_the_same_position()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
            new ExerciseOrderDto(ExerciseIdAt(1), 0),
            new ExerciseOrderDto(ExerciseIdAt(2), 1),
        ]));

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        (await ReadOrderAsync(databaseContext)).Select(row => row.Order).Should().Equal(0, 1, 2);
    }

    [Test]
    public async Task Reorder_refuses_the_same_exercise_named_twice()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
            new ExerciseOrderDto(ExerciseIdAt(0), 1),
            new ExerciseOrderDto(ExerciseIdAt(1), 2),
        ]));

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        (await ReadOrderAsync(databaseContext)).Select(row => row.Order).Should().Equal(0, 1, 2);
    }

    [Test]
    public async Task Reorder_refuses_an_exercise_belonging_to_another_lesson()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
            new ExerciseOrderDto(ExerciseIdAt(1), 1),
            new ExerciseOrderDto(ForeignExerciseId, 2),
        ]));

        response.Result.Should().BeOfType<BadRequestObjectResult>();

        // The foreign row is untouched — a reorder of one lesson cannot reach into another.
        var foreign = await databaseContext.Exercises.FindAsync(ForeignExerciseId);
        foreign!.OrderInLesson.Should().Be(0);
        foreign.LessonId.Should().Be(OtherLessonId);
    }

    [Test]
    public async Task Reorder_refuses_an_empty_request()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([]));

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        (await ReadOrderAsync(databaseContext)).Should().HaveCount(3);
    }

    [Test]
    public async Task Reorder_reports_a_lesson_that_does_not_exist_as_not_found()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(Guid.NewGuid(), new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
        ]));

        response.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task Reorder_accepts_the_order_it_already_has_without_changing_anything()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(0), 0),
            new ExerciseOrderDto(ExerciseIdAt(1), 1),
            new ExerciseOrderDto(ExerciseIdAt(2), 2),
        ]));

        response.Result.Should().BeOfType<OkObjectResult>();
        (await ReadOrderAsync(databaseContext)).Select(row => row.Id).Should()
            .Equal(ExerciseIdAt(0), ExerciseIdAt(1), ExerciseIdAt(2));
    }

    [Test]
    public async Task Reorder_accepts_non_contiguous_positions()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        // Lessons imported with 1-based or sparse numbering must stay reorderable: reads order by
        // this column rather than index into it, so only distinctness matters.
        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(2), 10),
            new ExerciseOrderDto(ExerciseIdAt(0), 20),
            new ExerciseOrderDto(ExerciseIdAt(1), 30),
        ]));

        response.Result.Should().BeOfType<OkObjectResult>();
        (await ReadOrderAsync(databaseContext)).Select(row => row.Id).Should()
            .Equal(ExerciseIdAt(2), ExerciseIdAt(0), ExerciseIdAt(1));
    }

    [Test]
    public async Task Reorder_returns_the_stored_content_of_each_exercise()
    {
        await using var databaseContext = await SeedLessonWithThreeExercisesAsync();
        var controller = CreateController(databaseContext);

        var response = await controller.Reorder(LessonId, new ReorderExercisesRequestDto([
            new ExerciseOrderDto(ExerciseIdAt(1), 0),
            new ExerciseOrderDto(ExerciseIdAt(0), 1),
            new ExerciseOrderDto(ExerciseIdAt(2), 2),
        ]));

        var returned = (response.Result as OkObjectResult)?.Value as IReadOnlyList<AdminExerciseDto>;
        returned.Should().NotBeNull();
        // The screen replaces its list from this response, so a stripped body would blank the rows.
        returned!.Should().AllSatisfy(exercise =>
            exercise.Content.GetProperty("question").GetString().Should().Be("q"));
        returned!.Select(exercise => exercise.Type).Should().AllBe("choose_option");
    }
}
