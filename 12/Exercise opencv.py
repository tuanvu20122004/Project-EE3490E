import cv2
import imutils
#Bai1
camera_id=0
cam=cv2.VideoCapture(camera_id)
while True:
    ret,frame=cam.read()
    if ret:
        #Tim diem trung tam
        #(h,w)
        width=frame.shape[1]
        height=frame.shape[0]
        center_x=width//2
        center_y=height//2
        #Tinh toan kich thuoc
        crop_x=int(width*0.2)
        crop_y=int(height*0.2)
        #khoanh vung anh
        left_top_x=center_x-crop_x//2
        left_top_y=center_y-crop_y//2
        crop_frame=frame[left_top_y:left_top_y+crop_y,left_top_x:left_top_x+crop_x]
        cv2.imshow("webcam",crop_frame)
    phim_bam=cv2.waitKey(1)
    if phim_bam==ord('q'):
        break
#BAI 2
video_file="anh\\video.mp4"
video=cv2.VideoCapture(video_file)
xoay=0
while True:
    ret1,frame1=video.read()
    if ret1:
        phim_bam1=cv2.waitKey(1)
        if phim_bam1==ord('q'):
            break
        elif phim_bam1==ord('a'):
            xoay=90
        elif phim_bam1==ord('d'):
            xoay=-90
        elif phim_bam1==ord(' '):
            xoay=0

        if xoay!=0:
            frame1=imutils.rotate(frame1,xoay)
        cv2.imshow("bai2",frame1)
#BAI 3
cam=cv2.VideoCapture(0)
nhan_dien=cv2.CascadeClassifier("haarcascade_frontalface_default.xml")
while True:
    ret,frame=cam.read()
    if ret:
        #Nhan dien anh xam
       anh_xam=cv2.cvtColor(frame,cv2.COLOR_BGR2GRAY)
       faces=nhan_dien.detectMultiScale(anh_xam,scaleFactor=1.05,minNeighbors=5,minSize=(30,30))
    for (x,y,w,h) in faces:
        cv2.rectangle(frame,(x,y),(x+w,y+h),(0,255,0),2)
    print("so anh la:",len(faces))
    cv2.imshow("webcam",frame)
    phim_bam=cv2.waitKey(1)
    if cv2.waitKey(1)==ord('q'):
        break
    elif phim_bam==ord('s'):
        dem=0
        for (x,y,w,h) in faces:
            crop_faces=frame[y:y+h,x:x+w]
            cv2.imwrite("anh\\{}.png".format(dem),crop_faces)
            dem+=1
cv2.destroyAllWindows()
cam.release()
#BAI 4
anh_doc_tu_file=cv2.imread("anh\\ballon.png")

#tim duong bao(contour)
#chuyen ve anh xam
anh_xam=cv2.cvtColor(anh_doc_tu_file,cv2.COLOR_BGR2GRAY)
#tim nguong
ret,nguong=cv2.threshold(anh_xam,230,255,cv2.THRESH_BINARY)
cv2.imshow("Anh nguong",nguong)
#dem so duong bao lon hon 1 gia tri dinh truoc ta da uoc luong
contours,_= cv2.findContours(nguong,cv2.RETR_LIST,cv2.CHAIN_APPROX_SIMPLE)
MIN_AREA=anh_doc_tu_file.shape[1] * anh_doc_tu_file.shape[0] / 150
dem=0
for cnt in contours[:-1]:
    if cv2.contourArea(cnt)>MIN_AREA:
       dem+=1
       cv2.drawContours(anh_doc_tu_file,[cnt],-1,(0,255,0),2,cv2.LINE_AA)
cv2.imshow("anh bong bay",anh_doc_tu_file)
print("So bong bay la :",dem)
cv2.waitKey()
cv2.destroyAllWindows()