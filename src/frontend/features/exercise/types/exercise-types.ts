/**
 * All supported exercise type identifiers.
 * Change values here to rename types across the entire frontend.
 */
export const ExerciseTypes = {
  ChooseOption: 'choose_option',
  FillBlank: 'fill_blank',
  Reorder: 'reorder',
  MatchPairs: 'match_pairs',
  Categorize: 'categorize',
  SpotMistake: 'spot_mistake',
  Rewrite: 'rewrite',
  AiDialogue: 'ai_dialogue',
  EvaluateCall: 'evaluate_call',
  FreeText: 'free_text',
  TheoryCard: 'theory_card',
} as const;

export type ExerciseType = typeof ExerciseTypes[keyof typeof ExerciseTypes];

export const AllExerciseTypes = Object.values(ExerciseTypes);

export const AiPoweredExerciseTypes: ExerciseType[] = [
  ExerciseTypes.SpotMistake,
  ExerciseTypes.Rewrite,
  ExerciseTypes.AiDialogue,
  ExerciseTypes.EvaluateCall,
  ExerciseTypes.FreeText,
];

export const isAiPoweredExercise = (type: ExerciseType): boolean =>
  AiPoweredExerciseTypes.includes(type);
