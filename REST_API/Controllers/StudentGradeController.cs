using DAL;
using JavaCodeChecker.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace REST_API.Controllers
{
    public class StudentGradeController : ApiController
    {
        [HttpGet]
        public IHttpActionResult Get(int id)
        {
            try
            {
                using (CodeCheckerContext entities = new CodeCheckerContext())
                {
                    var grade = entities.StudentGrades.Where(x => x.Id == id)?.ToList();
                    if (grade != null)
                    {
                        List<StudentGradeModel> gradesCollection = new List<StudentGradeModel>();

                        foreach (var item in grade)
                        {
                            StudentGradeModel model = new StudentGradeModel
                            {
                                //Id = item.Id,
                                //CourseName = item.CourseName,
                                //HomeworkAvg = item.HomeworkAvg,
                                //StudentId = item.StudentId,
                                //StudentName = item.StudentName,
                                //Year = item.Year
                            };

                            gradesCollection.Add(model);
                        }
                        return Ok(gradesCollection);
                    }
                    else
                    {
                        return Content(HttpStatusCode.NotFound, "No Student Data Available for this id");
                    }
                }
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadRequest, ex);
            }
        }

        [HttpPost]
        public HttpResponseMessage Post([FromBody] List<CourseAverageGrade> CourseAverageGrades)
        {
            try
            {
                var studentsGrades = new List<StudentGrade>();

                foreach (var studentGrade in CourseAverageGrades)
                {
                    studentsGrades.Add(new StudentGrade
                    {
                        CourseName = studentGrade.CourseName,
                        HomeworkAvg = studentGrade.AverageGrade,
                        Year = studentGrade.Year,
                        StudentId = Convert.ToInt32(studentGrade.StudentId),
                        StudentName = studentGrade.StudentName
                    });
                }
                using (CodeCheckerContext entities = new CodeCheckerContext())
                {
                    entities.StudentGrades.AddRange(studentsGrades);
                    entities.SaveChanges();
                    var res = Request.CreateResponse(HttpStatusCode.Created, studentsGrades);
                    res.Headers.Location = new Uri(Request.RequestUri + studentsGrades.FirstOrDefault().Id.ToString());
                    return res;
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        [HttpDelete]
        public HttpResponseMessage Delete(int id)
        {
            try
            {
                using (CodeCheckerContext entities = new CodeCheckerContext())
                {
                    var grade = entities.StudentGrades.Where(s => s.Id == id).FirstOrDefault();
                    if (grade != null)
                    {
                        entities.StudentGrades.Remove(grade);
                        entities.SaveChanges();
                        return Request.CreateResponse(HttpStatusCode.OK, "grade with id" + id + " Deleted");
                    }
                    else
                    {
                        return Request.CreateErrorResponse(HttpStatusCode.NotFound, "grade with id" + id + " is not found!");
                    }
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        [HttpPut]
        public HttpResponseMessage Put([FromBody] StudentGrade grade)
        {
            try
            {
                using (CodeCheckerContext entities = new CodeCheckerContext())
                {
                    var b = entities.StudentGrades.Where(em => em.Id == grade.Id).FirstOrDefault();
                    if (b != null)
                    {
                        b.StudentName = grade.StudentName;
                        b.StudentId = grade.StudentId;
                        b.CourseName = grade.CourseName;
                        b.Year = grade.Year;
                        b.HomeworkAvg = grade.HomeworkAvg;

                        entities.SaveChanges();
                        var res = Request.CreateResponse(HttpStatusCode.OK, "grade updated");
                        res.Headers.Location = new Uri(Request.RequestUri + grade.Id.ToString());
                        return res;
                    }
                    else
                    {
                        return Request.CreateErrorResponse(HttpStatusCode.NotFound, "grade is not found!");
                    }
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
    }
}