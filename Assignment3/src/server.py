from flask import Flask, request
import random
import joblib
import pandas as pd

app = Flask(__name__)


def get_features_for_n_seconds(df, timespan, label, participant_id):
    ''' Calculates the features for a raw gaze data in chunks of n seconds.
    Input: Dataframe with raw gaze data, timespan to chunk, label (i.e. activity class).
    Output: List of dictionaries, one dictionary contains a chunk of features.
    '''
    
    list_of_features = []
    i = 0
    while i < len(df)-1: 
        print(i)
        newdf = pd.DataFrame(columns=df.columns)
        start_time = int(df["eyeDataTimestamp"][i])
        # print(start_time)
        while i < len(df)-1 and int(df["eyeDataTimestamp"][i]) < (start_time+timespan*1000):
            entry = df.iloc[[i]]
            newdf = pd.concat([newdf,entry])
            i+=1
        newdf.reset_index(inplace = True)
        
        if (len(newdf) > timespan*28):
            newdf_valid = only_valid_data(newdf)
            df_fixations = get_fixation_df(newdf_valid)
            
            features = calculate_fixation_features(df_fixations, timespan)
            blinks = calculate_blink_features(newdf,timespan)

            directions = calculate_directions_of_list(df_fixations)            
            density = calculate_fixation_density(newdf_valid, df_fixations)
            
            features.update(blinks)
            features.update(directions)
            features.update(density)
            features["label"] = label
            # print(f"label: {label}")
            features["duration"] = timespan
            features["participant_id"] = participant_id            
            list_of_features.append(features)   
        
    return list_of_features


def calculate_fixation_density(df_all, df_fix):
    '''Calculates the fixation density per area.
    Input: Dataframe with all valid gazepoints, Dataframe with fixations.
    Output: Dict containing the fixation density.'''

    if not df_fix.empty:
        min_x = df_all['gazeDirection_x'].min()
        min_y = df_all['gazeDirection_y'].min()
        max_x = df_all['gazeDirection_x'].max()
        max_y = df_all['gazeDirection_y'].max()
        
        length = abs(max_x-min_x)
        height = abs(max_x-min_x)
        area = length*height
        
        number_of_fixations = len(df_fix)
        
        fix_dens = -1
        if area != 0: 
            fix_dens = number_of_fixations/area
        return {"fixDensPerBB": fix_dens}
    else:
        return {"fixDensPerBB": 0}

def calculate_directions_of_list(points):
    '''Calculates the dominant direction of points.
    Input: Dataframe containing fixation points.
    Output: Dict with dominant direction for x (xDir) and y (yDir).
    '''

    if not points.empty:
        x_values, y_values, z_values = zip(*points['centroid'])
        # Get a list of whether a given value is greater then the previous one in the list
        res_x = [float(val1) < float(val2) for val1, val2 in zip(x_values, x_values[1:])]
        # Sum all that are True
        sum_x = sum(res_x)
        # Divide the sum by the total number of values to get the desired output.
        # dir_x is -1 if there are no fixation (i.e. prevent division by zero)
        dir_x = -1
        if len(res_x) != 0:
            dir_x = sum_x/len(res_x)
            
        res_y = [float(val1) < float(val2) for val1, val2 in zip(y_values, y_values[1:])]
        sum_y = sum(res_y)
        dir_y = -1
        if len(res_y) != 0:
            dir_y = sum_y /len(res_y)
        
        
        return {"xDir": dir_x, "yDir": dir_y}

    else: 
        return {"xDir": 0, "yDir": 0}

def calculate_fixation_features(df_fixations, timespan):
    '''Calculates the fixation features. 
    Input: Dataframe with fixation, timespan of data chunk in seconds.
    Output: Dict containing the fixation features.'''

    if not df_fixations.empty:
        min_fix = df_fixations["duration"].min() 
        max_fix = df_fixations["duration"].max() 
        mean_fix = df_fixations["duration"].mean() 
        var_fix = df_fixations["duration"].var() 
        std_fix = df_fixations["duration"].std() 
        
        min_dispersion = df_fixations["dispersion"].min() 
        max_dispersion = df_fixations["dispersion"].max() 
        mean_dispersion = df_fixations["dispersion"].mean() 
        var_dispersion = df_fixations["dispersion"].var() 
        std_dispersion = df_fixations["dispersion"].std() 

        fixation_frequency_second = (len(df_fixations["dispersion"]) / timespan)
    else:
        min_fix = 0
        max_fix = 0
        mean_fix = 0
        var_fix = 0
        std_fix = 0
        
        min_dispersion = 0
        max_dispersion = 0
        mean_dispersion = 0
        var_dispersion = 0
        std_dispersion = 0

        fixation_frequency_second = 0
    
    # print("min: ", min_fix, " max: ", max_fix, " mean: ", mean_fix, " var: ", var_fix, "std: ", std_fix)
    # print("min dispersion: ", min_dispersion, " max: ", max_dispersion, " mean: ", mean_dispersion, 
    #      " var: ", var_dispersion)
    #print("x dispersion: ", dispersion_x, " y dispersion: ", dispersion_y, " z dispersion: ", dispersion_z)
    
    return {"meanFix": mean_fix, "minFix": min_fix, "maxFix": max_fix, "varFix": var_fix, "stdFix": std_fix,
            "meanDis": mean_dispersion, "minDis": min_dispersion, "maxDis": max_dispersion,
            "varDis": var_dispersion, "stdDisp": std_dispersion,
            "freqDisPerSec": fixation_frequency_second}
    

def get_fixation_df(df_valid):
    '''Calls function to calculate Fixations. Converts the list of fixations to a dataframe and numbers the rows as index.
     Input: Dataframe containg valid gaze points.
     Output: Dataframe containing the fixation features.'''
    fixations = list(detect_fixations(df_valid))
    df = pd.DataFrame(fixations)
    df['index'] = range(1, len(df) + 1)
    # df.head()
    return df

import datetime
def calculate_blink_features(df, timespan): 
    ''' Calculates the blink features for a given df of raw data.
    Input: Dataframe with raw data (incl. invalid points), timespan of data chunk in seconds\
    Output: Dict with the blink features
    '''
    i=0;
    blink_list = []
    blink_duration_list = []
    number_of_blinks = 0
    window_start_time = df["eyeDataTimestamp"][0]
    window_end_time = df["eyeDataTimestamp"][0]
    all_false = 0;
    if (not window_start_time  or not window_end_time):
        return {}
        
    for i, row in df.iterrows():    
        
        cur_number_of_blinks = 0
        if (not row["gazeHasValue"]):
            cur_number_of_blinks+=1
            all_false += 1
            while (i < len(df["gazeHasValue"])) and (not df["gazeHasValue"][i]) and  window_end_time - window_start_time < 1000:
                window_end_time = row["eyeDataTimestamp"]
                i+=1
                all_false +=1
            blink_list.append(cur_number_of_blinks)
            duration = window_end_time - window_start_time
            blink_duration_list.append(duration)
        
        number_of_blinks += cur_number_of_blinks;
        if  int(window_end_time) - int(window_start_time) > 1000:
            window_start_time = window_end_time
    
    blinks_per_second = 0
    if (len(blink_list)  > 0):
        blinks_per_second = number_of_blinks / timespan 
    avg_blink_duration = 0
    min_blink_duration = 0
    max_blink_duration = 0
    if (len(blink_duration_list) > 0):
        avg_blink_duration = sum(blink_duration_list) / len(blink_duration_list)
        min_blink_duration = min(blink_duration_list)
        max_blink_duration = max(blink_duration_list)
        
    # print("all_blinks: ", number_of_blinks, " avg_blink_duration: ", avg_blink_duration, 
    #    " min_blink_duration: ", min_blink_duration, " max_blink_duration: ", max_blink_duration,
    #  " blinks_per_second: ", blinks_per_second, " all false: ", all_false)
    
    return {"number_of_blinks":number_of_blinks, "blinkMean": avg_blink_duration, 
            "blinkMin": min_blink_duration, "blinkMax": max_blink_duration, 
            "blinkRate": blinks_per_second}



def only_valid_data(data):
    '''Returns only valid gaze points. Those have values in gazeDirection_x etc.'''
    return data[(data.gazeHasValue == True) & (data.isCalibrationValid == True)]

from collections import deque

def detect_fixations(gaze_data):
    # Convert Pandas data frame to list of Python dictionaries
    gaze_data = gaze_data.T.to_dict().values()

    candidate = deque()
    future_data = deque(gaze_data)
    while future_data:
        # check if candidate contains enough data
        if len(candidate) < 2 or candidate[-1]['eyeDataTimestamp'] - candidate[0]['eyeDataTimestamp'] < min_duration:
            datum = future_data.popleft()
            candidate.append(datum)
            continue

        # Minimal duration reached, check for fixation
        dispersion = gaze_dispersion(candidate)
        if dispersion > max_dispersion:
            # not a fixation, move forward
            candidate.popleft()
            continue

        # Minimal fixation found. Try to extend!
        while future_data:
            datum = future_data[0]
            candidate.append(datum)

            dispersion = gaze_dispersion(candidate)
            if dispersion > max_dispersion:
                # end of fixation found
                candidate.pop()
                break
            else:
                # still a fixation, continue extending
                future_data.popleft()
        centroid = get_centroid(candidate)
        yield {"start": candidate[0]['eyeDataTimestamp'], "end": candidate[-1]['eyeDataTimestamp'],
               "duration": candidate[-1]['eyeDataTimestamp'] - candidate[0]['eyeDataTimestamp'],
              "centroid": centroid, "dispersion": dispersion}
        candidate.clear()

def get_centroid(eye_data):
    '''Calculates the centroid for each point in a df of points.
    Input: Df of points.
    Output: Vector containg the centroid of all points.'''
    x = [p['gazeDirection_x'] for p in eye_data]
    y = [p['gazeDirection_y'] for p in eye_data]
    z = [p['gazeDirection_z'] for p in eye_data]
    return (sum(x) / len(eye_data), sum(y) / len(eye_data), sum(z) / len(eye_data))

def gaze_dispersion(eye_data):
    base_data = eye_data

    vectors = []
    for p in base_data:
        vectors.append((p['gazeDirection_x'], p['gazeDirection_y'], p['gazeDirection_z']))
    vectors = np.array(vectors, dtype=np.float32)

    if len(vectors) < 2:
        return float("inf")
    else:
        return vector_dispersion(vectors)  

import scipy.spatial.distance

def vector_dispersion(vectors):
    distances = scipy.spatial.distance.pdist(vectors, metric='cosine')
    distances.sort()
    cut_off = np.max([distances.shape[0] // 5, 4])
    return np.arccos(1. - distances[-cut_off:].mean())

import numpy as np

# spatial threshold or the dispersion itself
max_dispersion = np.deg2rad(1.6)
# temporal threshold or duration
min_duration = 100


def getFeatures(gazeData):
    # print(gazeData)
    gazeData = "eyeDataTimestamp,eyeDataRelativeTimestamp,frameTimestamp,isCalibrationValid,gazeHasValue,gazeOrigin_x,gazeOrigin_y,gazeOrigin_z,gazeDirection_x,gazeDirection_y,gazeDirection_z,gazePointHit,gazePoint_x,gazePoint_y,gazePoint_z,gazePoint_target_name,gazePoint_target_x,gazePoint_target_y,gazePoint_target_z,gazePoint_target_pos_x,gazePoint_target_pos_y,gazePoint_target_pos_z,gazePoint_target_rot_x,gazePoint_target_rot_y,gazePoint_target_rot_z,gazePoint_target_scale_x,gazePoint_target_scale_y,gazePoint_target_scale_z,gazePointLeftScreen_x,gazePointLeftScreen_y,gazePointLeftScreen_z,gazePointRightScreen_x,gazePointRightScreen_y,gazePointRightScreen_z,gazePointMonoScreen_x,gazePointMonoScreen_y,gazePointMonoScreen_z,GazePointWebcam_x,GazePointWebcam_y,GazePointWebcam_z,gazePointAOIHit,gazePointAOI_x,gazePointAOI_y,gazePointAOI_z,gazePointAOI_name,gazePointAOI_target_x,gazePointAOI_target_y,gazePointAOI_target_z,gazePointAOI_target_pos_x,gazePointAOI_target_pos_y,gazePointAOI_target_pos_z,gazePointAOI_target_rot_x,gazePointAOI_target_rot_y,gazePointAOI_target_rot_z,gazePointAOI_target_scale_x,gazePointAOI_target_scale_y,gazePointAOI_target_scale_z,GazePointAOIWebcam_x,GazePointAOIWebcam_y,GazePointAOIWebcam_z,GameObject_Main Camera_xPos,GameObject_Main Camera_yPos,GameObject_Main Camera_zPos,GameObject_Main Camera_xRot,GameObject_Main Camera_yRot,GameObject_Main Camera_zRot,GameObject_Main Camera_xScale,GameObject_Main Camera_yScale,GameObject_Main Camera_zScale,info;" + gazeData 
    gazeData = gazeData[:-1]
    df = pd.DataFrame([x.split(',') for x in gazeData.split(';')[1:]], columns=[x for x in gazeData.split(';')[0].split(',')])
    # df = pd.read_csv("./Data/RawGazeData/01_Inspection.csv")
    return get_features_for_n_seconds(df, 1, "unknown", "unknown")

@app.route("/", methods=['GET'])
def index():
  return "Hello World!"

@app.route("/", methods=['POST'])
def index_post():
    # print(request.form)
    # print(request.form.get('gazeData'))
    # print("DATA")
    print(request.json['gazeData'])

    return {
        "activity": "Search action",
        "probability": "0.87",
        "time": "2022-10-14T02:02:02Z",
    }

    linear = joblib.load('random_forest.joblib')
    f = pd.DataFrame(getFeatures(request.json['gazeData']))
    print(f[0:1])
    features = f[["meanFix", "maxFix", "varFix", "xDir", "yDir", "fixDensPerBB", "blinkMean", "blinkRate", "stdDisp", "varDis"]]
    prediction = linear.predict(features[-1:])
    print(prediction)

    return prediction[0]


if __name__ == "__main__":
  app.run(host= '0.0.0.0',debug=True, port=3002)