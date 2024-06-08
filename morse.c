#include <stdio.h>
#include <string.h>
#include <ctype.h>
#include <stdlib.h>
#include <unistd.h> // Để sử dụng hàm access()

#define MAX_LINE_LENGTH 1024
#define MAX_FILENAME_LENGTH 256

// Bảng mã Morse mở rộng
typedef struct {
    const char *morse;
    char ascii;
} MorseCode;

MorseCode morse_table[] = {
    {".-", 'a'},   {"-...", 'b'}, {"-.-.", 'c'}, {"-..", 'd'}, {".", 'e'},
    {"..-.", 'f'}, {"--.", 'g'},  {"....", 'h'}, {"..", 'i'},  {".---", 'j'},
    {"-.-", 'k'},  {".-..", 'l'}, {"--", 'm'},   {"-.", 'n'},  {"---", 'o'},
    {".--.", 'p'}, {"--.-", 'q'}, {".-.", 'r'},  {"...", 's'}, {"-", 't'},
    {"..-", 'u'},  {"...-", 'v'}, {".--", 'w'},  {"-..-", 'x'}, {"-.--", 'y'},
    {"--..", 'z'}, {"-----", '0'}, {".----", '1'}, {"..---", '2'},
    {"...--", '3'}, {"....-", '4'}, {".....", '5'}, {"-....", '6'},
    {"--...", '7'}, {"---..", '8'}, {"----.", '9'}, {"/", ' '}, {NULL, 0} // Kết thúc bảng mã
};

// Hàm chuyển ký tự ASCII sang mã Morse
const char* ascii_to_morse(char ascii) {
    for (int i = 0; morse_table[i].morse != NULL; i++) {
        if (morse_table[i].ascii == ascii) {
            return morse_table[i].morse;
        }
    }
    return NULL; // Ký tự không hợp lệ
}

// Hàm chuyển mã Morse sang ký tự ASCII
char morse_to_ascii(const char *morse) {
    for (int i = 0; morse_table[i].morse != NULL; i++) {
        if (strcmp(morse, morse_table[i].morse) == 0) {
            return morse_table[i].ascii;
        }
    }
    return '*'; // Ký tự không hợp lệ
}

// Hàm kiểm tra xem file có phải là Morse hay không
int is_morse_file(FILE *file) {
    int is_morse = 1;
    char ch;
    while ((ch = fgetc(file)) != EOF) {
        if (!isspace(ch) && ch != '.' && ch != '-' && ch != '/') {
            is_morse = 0;
            break;
        }
    }
    rewind(file); // Đưa con trỏ file về đầu
    return is_morse;
}

// Hàm kiểm tra xem một tập tin có tồn tại không
int file_exists(const char *filename) {
    return access(filename, F_OK) != -1;
}

// Hàm ghi đè file đầu ra nếu người dùng đồng ý
int overwrite_file(const char *filename) {
    printf("Warning: %s already exists. Do you wish to overwrite (y/n)? ", filename);
    char response;
    scanf(" %c", &response);
    if (response == 'y' || response == 'Y') {
        return 1;
    } else {
        return 0;
    }
}

// Hàm xử lý lỗi khi không mở được file
void file_open_error(const char *filename) {
    fprintf(stderr, "Error: %s could not be opened.\n", filename);
}

// Hàm đọc file Morse và ghi ra file text
void decode_morse_file(FILE *inputFile, FILE *outputFile) {
    char line[MAX_LINE_LENGTH];
    int line_number = 0;
    while (fgets(line, sizeof(line), inputFile)) {
        line_number++;
        // Loại bỏ ký tự xuống dòng nếu có
        size_t len = strlen(line);
        if (len > 0 && line[len - 1] == '\n') {
            line[len - 1] = '\0';
        }

        char *token = strtok(line, " ");
        while (token) {
            if (strcmp(token, "........") == 0) {
                fputc('#', outputFile);
            } else {
                char ascii = morse_to_ascii(token);
                if (ascii != '*') {
                    fputc(ascii, outputFile);
                } else {
                    fprintf(stderr, "Error: Invalid Morse code %s on line %d\n", token, line_number);
                    fputc('*', outputFile);
                }
            }
            token = strtok(NULL, " ");
        }
        fputc('\n', outputFile); // Xuống dòng sau mỗi dòng của file input
    }
}

// Hàm đọc file text và ghi ra file Morse
void encode_text_file(FILE *inputFile, FILE *outputFile) {
    char line[MAX_LINE_LENGTH];
    int line_number = 0;
    while (fgets(line, sizeof(line), inputFile)) {
        line_number++;
        // Loại bỏ ký tự xuống dòng nếu có
        size_t len = strlen(line);
        if (len > 0 && line[len - 1] == '\n') {
            line[len - 1] = '\0';
        }

        for (size_t i = 0; i < strlen(line); i++) {
            if (line[i] == '#') {
                fputs("........ ", outputFile);
            } else {
                char ch = tolower(line[i]);
                const char *morse = ascii_to_morse(ch);
                if (morse) {
                    fputs(morse, outputFile);
                    fputc(' ', outputFile);
                } else if (!isspace(ch)) {
                    fprintf(stderr, "Error: Unrecognised character %c on line %d\n", ch, line_number);
                }
            }
        }
        fputc('\n', outputFile); // Xuống dòng sau mỗi dòng của file input
    }
}

int main() {
    const char *input_filename = "myinput.dat";
    const char *output_filename = "output.txt";

    // Kiểm tra file đầu vào
    FILE *inputFile = fopen(input_filename, "r");
    if (inputFile == NULL) {
        file_open_error(input_filename);
        return 1;
    }

    // Kiểm tra xem file đầu ra đã tồn tại chưa
    if (file_exists(output_filename)) {
        if (!overwrite_file(output_filename)) {
           
            fclose(inputFile);
            return 0; // Kết thúc chương trình
        }
    }

    // Mở file output để ghi
    FILE *outputFile = fopen(output_filename, "w");
    if (outputFile == NULL) {
        file_open_error(output_filename);
        fclose(inputFile);
        return 1;
    }

    // Kiểm tra file input là Morse hay text và xử lý tương ứng
    if (is_morse_file(inputFile)) {
        decode_morse_file(inputFile, outputFile);
    } else {
        encode_text_file(inputFile, outputFile);
    }

    // Đóng cả hai file
    fclose(inputFile);
    fclose(outputFile);

    printf("Conversion complete. Check %s for the result.\n", output_filename);
    return 0;
}
