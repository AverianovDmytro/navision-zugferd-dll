# Jetbrains AI Prompts

Inspect the requirements document. Analyze for accuracy and completeness. Make recommendations for
how we can improve this document. Implement the improvements in a revised version.


--------------------
Can the Guide outline be improved?

---------
Inspect the requirements.md file. Generate a prompt to create an implementation plan from this file.

# Junie Prompts

Inspect the file `prompts/requirements-draft.md`. Update and improve this developer guide using the context
of this project. The guide should be organized into clear actionable steps.
Write the improved guide to `prompts/requirements.md`.

-------------------------

**THIS DID NOT WORK**
Analyze the `prompts/requirements.md` file and create a detailed plan for the improvements of this project.
Write the plan to `prompts/plan.md` file.



Create a detailed enumerated task list according to the suggested enhancements plan in
`prompts/plan.md` Task items should have a placeholder [ ] for marking as done [x] upon task completion.
Write the task list to `prompts/tasks.md` file.

-------------------------------------

Complete the task list `prompts/tasks.md`. Use information from `prompts/requirements.md` and `prompts/plan.md` for
additional context when completing the tasks.

Implement the tasks in the task list. Focus on completing the tasks in order. Mark the task complete as it is done
using [x]. As each step is completed, it is very important to update the task list mark and the task as done [x].

-----------------------------------
**RUN THIS IN ASK MODE**
Inspect the files `prompts/requirements.md` and `prompts/plan.md`. These changes have been implemented in the project.
Review the project as needed. Plan additional sections in the guideline.md file for the changes which have been
implemented in the project. Include instructions for the project structure and for building and testing the frontend project.
Also identify any best practices used for the front end code.

-----------------------------------
**CHANGE BACK TO CODE MODE**
The frontend project has build errors. Fix errors, verify tests are passing.

-----------------------------------

The command `npm test` is failing, fix test errors, verify all tests are passing

-----------------------------------

The command `npm line` is shows lint errors, inspect the lint errors and fix, verify there are no lint errors

-----------------------------------
Update eslint configuration to disable the warning for `Unused eslint-disable directive`

# Project Building Without Errors

In the frontend project, running the command `npm build` produces the following error, indicating
a problem with tailwindcss. Inspect error and project to correct the problem.

------------------------

Running the front end application produces the following error. Inspect error. Fix frontend
code and tests. Verify the frontend project builds and all tests are passing.

-----------------
Inspect the frontend project. The styling for radix and shadcn is not working properly. Make necesary
updates to fix. Verify the project builds and tests without errors.

-------------------
Inspect the frontend project. The styling for radix and shadcn is not working properly. Verify tailwindcss
version 4 is properly setup for use with radix and shadcn components. Verify plugins for radix are
installed and configured properly. Make necessary updates to fix.
Verify the project builds and tests without errors.

-----------------------

I möchte das Frontend auf http://localhost:8084 sehen.
Machst Spring Boot die gebauten Frontend-Dateien (index.html, JS/CSS) automatisch aus dem Ordner src/main/resources/static ausliefert.
Mein Vite-Build soll genau dort die Dateien ablegen.
Damit bedient das Backend (Port 8084) die SPA gleich mit.