using EPAMS.Models.DTO;
using EPAMS.Models;
//using EPAMS.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace FYP.Controllers.DIRECTOR
{
    [RoutePrefix("api/Performance")]
    public class PerformanceController : ApiController
    {
        EPAMSEntities db = new EPAMSEntities();

        // ✅ 1. Get All Sessions (Dropdown)
        [HttpGet]
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Sessions
                .Select(s => new
                {
                    id = s.id,
                    name = s.name
                }).ToList();

            return Ok(sessions);
        }

        // ✅ 2. Get Employee Types (Tabs: Teacher, Admin, etc.)
        [HttpGet]
        [Route("GetEmployeeTypes")]
        public IHttpActionResult GetEmployeeTypes()
        {
            var types = db.EmployeeTypes
                .Select(e => new
                {
                    id = e.id,
                    type = e.type
                }).ToList();

            return Ok(types);
        }

        // ✅ 3. Get Courses based on Session
        [HttpGet]
        [Route("GetCoursesBySession")]
        public IHttpActionResult GetCoursesBySession(int sessionId)
        {
            var courses = db.Enrollments
                .Where(e => e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();

            return Ok(courses);
        }

        [HttpGet]
        [Route("GetTeacherPerformance")]
        public IHttpActionResult GetTeacherPerformance(int sessionId, string department = null, string courseCode = null)
        {
            var query = db.Enrollments.Where(e => e.sessionID == sessionId);

            if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
            {
                query = query.Where(e => e.courseCode == courseCode);
            }

            var data = query
                .GroupBy(e => new { e.teacherID, e.courseCode })
                .Select(g => new
                {
                    TeacherID = g.Key.teacherID,
                    CourseCode = g.Key.courseCode,

                    TeacherName = db.Teachers
                        .Where(t => t.userID == g.Key.teacherID)
                        .Select(t => t.name)
                        .FirstOrDefault(),

                    Department = db.Teachers
                        .Where(t => t.userID == g.Key.teacherID)
                        .Select(t => t.department)
                        .FirstOrDefault(),

                    // ✅ Peer Evaluation Avg
                    PeerAvg = db.PeerEvaluations
                        .Where(p =>
                            p.evaluateeID == g.Key.teacherID &&
                            p.courseCode == g.Key.courseCode &&
                            p.SessionID == sessionId
                        )
                        .Average(p => (int?)p.score),

                    // ✅ Student Evaluation Avg (FIXED JOIN)
                    StudentAvg = db.StudentEvaluations
                        .Where(s =>
                            s.SessionID == sessionId &&
                            s.Enrollment.teacherID == g.Key.teacherID &&
                            s.Enrollment.courseCode == g.Key.courseCode
                        )
                        .Average(s => (int?)s.score)
                })
                .ToList();

            // ✅ APPLY DEPARTMENT FILTER
            if (!string.IsNullOrEmpty(department))
            {
                data = data.Where(d => d.Department == department).ToList();
            }

            // ✅ FINAL RESULT WITH COMBINED AVERAGE
            var result = data.Select(x =>
            {
                var peer = x.PeerAvg ?? 0;
                var student = x.StudentAvg ?? 0;

                int count = 0;
                if (x.PeerAvg != null) count++;
                if (x.StudentAvg != null) count++;

                var finalAvg = count > 0 ? (peer + student) / count : 0;

                return new
                {
                    x.TeacherID,
                    x.TeacherName,
                    x.CourseCode,
                    x.Department,
                    Percentage = (finalAvg / 4.0) * 100
                };
            });

            return Ok(result);
        }

        [HttpGet]
        [Route("GetAllCourses")]
        public IHttpActionResult GetAllCourses()
        {
            var courses = db.Courses
                .Select(c => c.code)
                .Distinct()
                .ToList();

            return Ok(courses);
        }




        [HttpGet]
        [Route("GetTeachersByCourse")]
        public IHttpActionResult GetTeachersByCourse(string courseCode)
        {
            if (string.IsNullOrEmpty(courseCode))
                return Ok(new List<object>());

            courseCode = courseCode.Trim().ToUpper(); // normalize

            // Get latest session ID for this course
            var latestSessionId = db.Enrollments
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode)
                .OrderByDescending(e => e.sessionID)
                .Select(e => e.sessionID)
                .FirstOrDefault();

            var teachers = db.Enrollments
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode && e.sessionID == latestSessionId)
                .Select(e => new
                {
                    id = e.teacherID,
                    name = db.Teachers
                                .Where(t => t.userID == e.teacherID)
                                .Select(t => t.name)
                                .FirstOrDefault()
                })
                .Distinct()
                .ToList();

            return Ok(teachers);
        }



        [HttpGet]
        [Route("GetAllTeachers")]
        public IHttpActionResult GetAllTeachers()
        {
            var teachers = db.Enrollments
                .Select(e => new
                {
                    id = e.teacherID,
                    name = db.Teachers
                                .Where(t => t.userID == e.teacherID)
                                .Select(t => t.name)
                                .FirstOrDefault()
                })
                .Distinct()
                .ToList();

            return Ok(teachers);
        }


        [HttpPost]
        [Route("CompareTeachers")]
        public IHttpActionResult CompareTeachers(CompareDTO dto)
        {
            var result = new List<object>();

            if (dto.mode == "course")
            {
                // ✅ Get latest session for this course
                var latestSessionId = db.Enrollments
                    .Where(e => e.courseCode == dto.courseCode)
                    .OrderByDescending(e => e.sessionID)
                    .Select(e => e.sessionID)
                    .FirstOrDefault();

                result.Add(GetTeacherScore(dto.teacherA, dto.courseCode, latestSessionId));
                result.Add(GetTeacherScore(dto.teacherB, dto.courseCode, latestSessionId));
            }
            else
            {
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session1));
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session2));
            }

            return Ok(result);
        }

        private object GetTeacherScore(string teacherId, string courseCode, int? sessionId)
        {
            const double MAX_SCORE_PER_QUESTION = 4.0;  // Each question max points
            const double SCALE_TO_TEN = 10.0;           // Average score scaled out of 10

            // --- Peer Evaluation ---
            var peerList = db.PeerEvaluations
                .Where(p => p.evaluateeID == teacherId &&
                       (courseCode == null || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                       (sessionId == null || p.SessionID == sessionId))
                .ToList();

            double peerTotalScore = peerList.Sum(p => (double)p.score);
            double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
            double peerAverageOutOfTen = peerMaxTotal > 0 ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN : 0;

            // --- Student Evaluation ---
            var studentList = db.StudentEvaluations
                .Where(s =>
                    (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                    (sessionId == null || s.SessionID == sessionId) &&
                    s.Enrollment.teacherID == teacherId
                )
                .ToList();

            double studentTotalScore = studentList.Sum(s => (double)s.score);
            double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
            double studentAverageOutOfTen = studentMaxTotal > 0 ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN : 0;

            // --- Overall Average Out of 100 ---
            double overallAveragePercentage = 0;
            double totalScore = peerTotalScore + studentTotalScore;
            double totalMax = peerMaxTotal + studentMaxTotal;
            if (totalMax > 0)
                overallAveragePercentage = (totalScore / totalMax) * 100;

            // --- Teacher Name ---
            var name = db.Teachers
                .Where(t => t.userID == teacherId)
                .Select(t => t.name)
                .FirstOrDefault();

            return new
            {
                Name = name,
                PeerAverageOutOfTen = Math.Round(peerAverageOutOfTen, 2),
                StudentAverageOutOfTen = Math.Round(studentAverageOutOfTen, 2),
                OverallAverageOutOfHundred = Math.Round(overallAveragePercentage, 2),
                PeerTotalScore = Math.Round(peerTotalScore, 2),
                PeerMaxTotal = peerMaxTotal,
                StudentTotalScore = Math.Round(studentTotalScore, 2),
                StudentMaxTotal = studentMaxTotal,
                TotalScore = Math.Round(totalScore, 2),
                TotalMax = totalMax,
            };
        }





        [HttpGet]
        [Route("GetTeacherQuestionStatsFull")]
        public IHttpActionResult GetTeacherQuestionStatsFull(string teacherId, int sessionId, string evaluationType, string courseCode = null)
        {
            var result = new List<object>();

            // =========================
            // 🔹 STUDENT EVALUATION
            // =========================
            if (evaluationType == "student" || evaluationType == "both")
            {
                var studentQuery = db.StudentEvaluations
                    .Where(s =>
                        s.SessionID == sessionId &&
                        s.Enrollment.teacherID == teacherId &&
                        (courseCode == null || s.Enrollment.courseCode == courseCode)
                    );

                var studentData = studentQuery
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Student"
                    })
                    .ToList();

                result.AddRange(studentData);
            }

            // =========================
            // 🔹 PEER EVALUATION
            // =========================
            if (evaluationType == "peer" || evaluationType == "both")
            {
                var peerQuery = db.PeerEvaluations
                    .Where(p =>
                        p.SessionID == sessionId &&
                        p.evaluateeID == teacherId &&
                        (courseCode == null || p.courseCode == courseCode)
                    );

                var peerData = peerQuery
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Peer"
                    })
                    .ToList();

                result.AddRange(peerData);
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetTeacherResultByCourse")]
        public IHttpActionResult GetTeachersResultByCourse(string teacherId, string courseCode, int sessionId)
        {
            const double MAX_SCORE_PER_QUESTION = 4.0;
            const double SCALE_TO_TEN = 10.0;

            // --- Peer Evaluation ---
            var peerList = db.PeerEvaluations
                .Where(p => p.evaluateeID == teacherId &&
                       (courseCode == null || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                       (sessionId == null || p.SessionID == sessionId))
                .ToList();

            double peerTotalScore = peerList.Sum(p => (double)p.score);
            double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
            double peerAverageOutOfTen = peerMaxTotal > 0
                ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN
                : 0;

            // --- Student Evaluation ---
            var studentList = db.StudentEvaluations
                .Where(s =>
                    (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                    (sessionId == null || s.SessionID == sessionId) &&
                    s.Enrollment.teacherID == teacherId
                )
                .ToList();

            double studentTotalScore = studentList.Sum(s => (double)s.score);
            double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
            double studentAverageOutOfTen = studentMaxTotal > 0
                ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN
                : 0;

            // --- Overall ---
            double totalScore = peerTotalScore + studentTotalScore;
            double totalMax = peerMaxTotal + studentMaxTotal;

            double overallPercentage = totalMax > 0
                ? (totalScore / totalMax) * 100
                : 0;

            var name = db.Teachers
                .Where(t => t.userID == teacherId)
                .Select(t => t.name)
                .FirstOrDefault();

            return Ok(new
            {
                Name = name,
                PeerAverage = Math.Round(peerAverageOutOfTen, 2),
                StudentAverage = Math.Round(studentAverageOutOfTen, 2),
                Percentage = Math.Round(overallPercentage, 2),

                PeerTotal = peerTotalScore,
                PeerMax = peerMaxTotal,

                StudentTotal = studentTotalScore,
                StudentMax = studentMaxTotal,

                Total = totalScore,
                TotalMax = totalMax
            });
        }


        [HttpGet]
        [Route("GetTeachersPerformanceList")]
        public IHttpActionResult GetTeachersPerformanceList(int sessionId, string department = "All", string courseCode = "All")
        {
            var query = db.Enrollments.Where(e => e.sessionID == sessionId);

            if (department != "All") query = query.Where(e => e.Teacher.department == department);
            if (courseCode != "All") query = query.Where(e => e.courseCode == courseCode);

            var teacherIds = query.Select(e => e.teacherID).Distinct().ToList();
            var finalData = new List<object>();

            foreach (var tid in teacherIds)
            {
                // Yahan aap apna CalculatePerformance logic call karein
                // Isme ConfidentialEvaluation ka logic bhi add karein
                var perf = CalculatePerformance(tid, sessionId);
                finalData.Add(perf);
            }
            return Ok(finalData);
        }

        // Helper method (Taake code duplicate na ho)
        private object CalculatePerformance(string teacherId, int sessionId)
        {
            const double MAX = 4.0;
            const double SCALE = 10.0;

            // 1. Student Evaluations
            var studentList = db.StudentEvaluations
                .Where(s => s.Enrollment.teacherID == teacherId && s.Enrollment.sessionID == sessionId)
                .ToList();
            double sTotal = studentList.Sum(s => (double)s.score);
            double sMax = studentList.Count * MAX;
            double sAvg = sMax > 0 ? (sTotal / sMax) * SCALE : 0;

            // 2. Peer Evaluations
            var peerList = db.PeerEvaluations
                .Where(p => p.evaluateeID == teacherId && p.PeerEvaluator.sessionID == sessionId)
                .ToList();
            double pTotal = peerList.Sum(p => (double)p.score);
            double pMax = peerList.Count * MAX;
            double pAvg = pMax > 0 ? (pTotal / pMax) * SCALE : 0;

            // ✅ 3. CHR — Enrollment se session verify
            var isEnrolled = db.Enrollments
                .Any(e => e.teacherID == teacherId && e.sessionID == sessionId);

            // 3. CHR Average Score — Session filter ke saath
            // Sirf us session ki CHR records consider hongi
            var chrAvg = 0.0;

            var chrRawData = db.CHRs
                .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
                .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                .ToList();

            chrAvg = chrRawData.Any()
                ? chrRawData.Select(x => {
                    int total = x.LateIn + x.LeftEarly;
                    if (total >= 10) return 0.0;
                    if (total >= 6) return 3.0;
                    if (total >= 1) return 4.0;
                    return 5.0;
                }).Average()
                : 0.0;

            // CHR ko 10 scale pe convert karo (baaki scores ki tarah)
            double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

            var teacher = db.Teachers.FirstOrDefault(t => t.userID == teacherId);

            return new
            {
                TeacherID = teacherId,
                Name = teacher?.name,
                StudentAverage = Math.Round(sAvg, 2),
                PeerAverage = Math.Round(pAvg, 2),
                ChrAverage = chrPerc,           // ✅ CHR score 0-10 scale
                ChrRawScore = Math.Round(chrAvg, 2), // ✅ Raw score 0-5 scale
                CourseCode = db.Enrollments
                    .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                    .Select(e => e.courseCode).FirstOrDefault()
            };
        }


    }
}